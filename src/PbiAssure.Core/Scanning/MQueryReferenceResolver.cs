namespace PbiAssure.Core.Scanning;

internal sealed record MQueryReferenceResult(string[] References, bool Incomplete, bool Dynamic);

/// <summary>
/// Bounded expression walk retaining only references and lexical environments, not an evaluable AST.
/// Unsupported syntax fails closed for orphan confidence. Binding sets are completed before resolution,
/// so forward references and record sibling environments do not depend on declaration order.
/// </summary>
internal sealed class MQueryReferenceResolver
{
    private sealed record Scope(Scope? Parent, HashSet<string> Names, string? Initializing = null);
    private sealed record Reference(string Name, Scope? Scope, bool Inclusive = false);
    private sealed class UnsupportedSyntaxException : Exception;
    private readonly MReferenceToken[] tokens;
    private readonly List<Reference> references = [];
    private int position;
    private int depth;

    private MQueryReferenceResolver(MReferenceToken[] tokens) => this.tokens = tokens;

    public static MQueryReferenceResult Analyze(string expression, IReadOnlyCollection<string> knownNames)
    {
        var (tokens, incomplete) = MReferenceTokenizer.Tokenize(expression);
        var walker = new MQueryReferenceResolver(tokens);
        try
        {
            walker.Expression(null);
            if (walker.Current.Kind != MReferenceTokenKind.End) incomplete = true;
        }
        catch (UnsupportedSyntaxException) { incomplete = true; }

        var external = walker.references.Where(reference => !IsBound(reference)).Select(reference => reference.Name)
            .ToHashSet(StringComparer.Ordinal);
        // Dynamic name discovery cannot prove the absence of a reference to any other query in the model.
        var dynamic = external.Overlaps(["Expression.Evaluate", "Record.Field", "#shared", "#sections"]);
        // Identity and matching stay Ordinal because M identifiers are case-sensitive. Ordering is
        // presentation only, so it keeps the case-insensitive sort the previous extractor documented.
        return new((incomplete ? [] : knownNames.Where(external.Contains)).Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray(), incomplete, dynamic);
    }

    private static bool IsBound(Reference reference)
    {
        for (var scope = reference.Scope; scope is not null; scope = scope.Parent)
            if (scope.Names.Contains(reference.Name) && (reference.Inclusive || scope.Initializing != reference.Name)) return true;
        return false;
    }

    private MReferenceToken Current => tokens[position];
    private bool At(string text) => Current.Text == text && Current.Kind is MReferenceTokenKind.Symbol or MReferenceTokenKind.Keyword;
    private bool Take(string text) { if (!At(text)) return false; position++; return true; }
    private void Require(string text) { if (!Take(text)) throw new UnsupportedSyntaxException(); }
    private string Identifier()
    {
        if (Current.Kind is not (MReferenceTokenKind.Identifier or MReferenceTokenKind.QuotedIdentifier)) throw new UnsupportedSyntaxException();
        return tokens[position++].Text;
    }

    private void Expression(Scope? scope, int minimum = 0)
    {
        if (++depth > 128) throw new UnsupportedSyntaxException();
        Prefix(scope);
        while (true)
        {
            if (At("(") && minimum <= 12) { Arguments(scope, "(", ")"); continue; }
            if (Take("[")) { Selector(); continue; }
            if (At("{") && minimum <= 12) { Arguments(scope, "{", "}"); Take("?"); continue; }
            var priority = Priority(Current);
            if (priority < minimum || priority < 0) break;
            var operation = tokens[position++].Text;
            if (operation is "as" or "is") Type(scope);
            else Expression(scope, priority + 1);
        }
        depth--;
    }

    private void Prefix(Scope? scope)
    {
        if (Take("let"))
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            var local = new Scope(scope, names);
            do
            {
                var name = Identifier();
                if (!names.Add(name)) throw new UnsupportedSyntaxException();
                Require("=");
                Expression(new Scope(scope, names, name));
            } while (Take(","));
            Require("in");
            Expression(local);
        }
        else if (Take("each")) Expression(new Scope(scope, new HashSet<string>(StringComparer.Ordinal) { "_" }));
        else if (Take("if")) { Expression(scope); Require("then"); Expression(scope); Require("else"); Expression(scope); }
        else if (Take("try"))
        {
            Expression(scope);
            if (Take("otherwise")) Expression(scope);
            else if (Current.Kind == MReferenceTokenKind.Identifier && Current.Text == "catch")
            { position++; Expression(scope); }
        }
        else if (Take("error")) Expression(scope);
        else if (Take("not") || Take("+") || Take("-")) Expression(scope, 10);
        else if (Take("type")) Type(scope);
        else if (At("(") && IsFunction()) Function(scope);
        else if (Take("(")) { Expression(scope); Require(")"); }
        else if (At("{")) Arguments(scope, "{", "}");
        else if (Take("[")) RecordOrImplicitSelector(scope);
        else if (Take("@")) references.Add(new Reference(Identifier(), scope, Inclusive: true));
        else if (Current.Kind is MReferenceTokenKind.Identifier or MReferenceTokenKind.QuotedIdentifier) references.Add(new Reference(Identifier(), scope));
        else if (Current.Kind is MReferenceTokenKind.Number or MReferenceTokenKind.Text || At("null") || At("true") || At("false")) position++;
        else throw new UnsupportedSyntaxException();
    }

    private static int Priority(MReferenceToken token) => token.Kind is not (MReferenceTokenKind.Symbol or MReferenceTokenKind.Keyword) ? -1 : token.Text switch
    {
        "??" => 0, "or" => 1, "and" => 2, "is" => 3, "as" => 4,
        "=" or "<>" => 5, "<" or ">" or "<=" or ">=" => 6, "+" or "-" or "&" => 7,
        "*" or "/" => 8, "meta" => 9, ".." => 10, _ => -1,
    };

    private void Arguments(Scope? scope, string open, string close)
    {
        Require(open);
        if (Take(close)) return;
        do { Expression(scope); } while (Take(","));
        Require(close);
    }

    private string FieldName()
    {
        if (Current.Kind is not (MReferenceTokenKind.Identifier or MReferenceTokenKind.QuotedIdentifier or MReferenceTokenKind.Keyword or MReferenceTokenKind.Number))
            throw new UnsupportedSyntaxException();
        var name = tokens[position++].Text;
        while (Current.Kind is MReferenceTokenKind.Identifier or MReferenceTokenKind.Keyword or MReferenceTokenKind.Number)
        {
            // Generalized identifiers may contain spaces; quoted tokens are already decoded atomically.
            name += tokens[position - 1].End == Current.Start ? Current.Text : " " + Current.Text;
            position++;
        }
        return name;
    }

    private void RecordOrImplicitSelector(Scope? scope)
    {
        if (Take("]")) return;
        if (At("[")) { references.Add(new Reference("_", scope)); Selector(); return; }
        var name = FieldName();
        if (Take("]")) { references.Add(new Reference("_", scope)); Take("?"); return; }
        Require("=");
        var names = new HashSet<string>(StringComparer.Ordinal) { name };
        Expression(new Scope(scope, names, name));
        while (Take(","))
        {
            name = FieldName();
            if (!names.Add(name)) throw new UnsupportedSyntaxException();
            Require("=");
            Expression(new Scope(scope, names, name));
        }
        Require("]");
    }

    private void Selector()
    {
        if (Take("["))
        {
            FieldName(); Require("]");
            while (Take(",")) { Require("["); FieldName(); Require("]"); }
        }
        else FieldName();
        Require("]");
        Take("?");
    }

    private bool IsFunction()
    {
        var cursor = position;
        var nesting = 0;
        do
        {
            if (tokens[cursor].Kind == MReferenceTokenKind.End) return false;
            if (tokens[cursor].Text == "(" && tokens[cursor].Kind == MReferenceTokenKind.Symbol) nesting++;
            if (tokens[cursor].Text == ")" && tokens[cursor].Kind == MReferenceTokenKind.Symbol) nesting--;
            cursor++;
        } while (nesting > 0);
        if (tokens[cursor].Text == "=>") return true;
        // Function return annotations in the supported subset are primitive/nullable primitive types.
        if (tokens[cursor].Text != "as") return false;
        cursor++;
        if (tokens[cursor].Text == "nullable") cursor++;
        return cursor + 1 < tokens.Length && tokens[cursor + 1].Text == "=>";
    }

    private void Function(Scope? scope)
    {
        Require("(");
        var names = new HashSet<string>(StringComparer.Ordinal);
        if (!Take(")"))
        {
            do
            {
                if (Current.Text == "optional" && Current.Kind == MReferenceTokenKind.Identifier) position++;
                if (!names.Add(Identifier())) throw new UnsupportedSyntaxException();
                if (Take("as")) Type(scope);
            } while (Take(","));
            Require(")");
        }
        if (Take("as")) Type(scope);
        Require("=>");
        Expression(new Scope(scope, names));
    }

    private void Type(Scope? scope)
    {
        if (++depth > 128) throw new UnsupportedSyntaxException();
        if (Current.Text == "nullable" && Current.Kind == MReferenceTokenKind.Identifier) position++;
        // Function-type parameter declarations need a separate type grammar, not expression arguments.
        if (Current.Text == "function" && tokens[position + 1].Text == "(") throw new UnsupportedSyntaxException();
        if (Current.Text == "table" && Current.Kind == MReferenceTokenKind.Identifier && tokens[position + 1].Text == "[") position++;
        if (Take("["))
        {
            if (!Take("]"))
            {
                do
                {
                    if (Current.Text == "optional" && Current.Kind == MReferenceTokenKind.Identifier) position++;
                    FieldName(); Require("="); Type(scope);
                } while (Take(","));
                Require("]");
            }
        }
        else if (Take("{")) { Type(scope); Require("}"); }
        else if (Current.Text is "any" or "anynonnull" or "binary" or "date" or "datetime" or "datetimezone" or "duration"
            or "function" or "list" or "logical" or "none" or "null" or "number" or "record" or "table" or "text" or "time" or "type") position++;
        else if (Current.Kind is MReferenceTokenKind.Identifier or MReferenceTokenKind.QuotedIdentifier) Expression(scope, 11);
        else throw new UnsupportedSyntaxException();
        depth--;
    }
}
