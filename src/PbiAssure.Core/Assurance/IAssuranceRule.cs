using PbiAssure.Core.Inventory;

namespace PbiAssure.Core.Assurance;

internal interface IAssuranceRule
{
    IEnumerable<AssuranceFinding> Evaluate(ProjectInventory inventory);
}
