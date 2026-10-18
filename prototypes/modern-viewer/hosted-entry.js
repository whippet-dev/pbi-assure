// Narrow integration boundary. Standalone prototype use still loads its repository fixture.
export const hosted = document.documentElement.hasAttribute("data-hosted-investigation");

export function receiveAnalysis() {
  return new Promise((resolve, reject) => {
    const source = window.opener;
    if (!source) return reject(new Error("This URL does not contain a saved analysis. Open an investigation from your project after analysing it."));
    const cleanup = () => {
      clearInterval(ready);
      clearTimeout(timeout);
      window.removeEventListener("message", receive);
    };
    const receive = event => {
      if (event.origin !== location.origin || event.source !== source || event.data?.type !== "pbi-assure-investigation") return;
      cleanup();
      const message = event.data;
      resolve({...message, acknowledge: accepted => source.postMessage({
        type: "pbi-assure-investigation-ack", deliveryId: message.deliveryId, accepted,
      }, location.origin)});
    };
    window.addEventListener("message", receive);
    const ready = setInterval(() => source.postMessage({type: "pbi-assure-investigation-ready"}, location.origin), 100);
    const timeout = setTimeout(() => {
      cleanup();
      reject(new Error("The analysis did not arrive. Return to your project and open the investigation again."));
    }, 15000);
  });
}

export function returnToProject(event) {
  if (hosted && window.opener && !window.opener.closed) {
    event.preventDefault();
    window.opener.focus();
  }
}
