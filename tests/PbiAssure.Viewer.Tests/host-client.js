let snapshot, pending;
document
  .querySelector("#snapshot")
  .addEventListener("change", async (event) => {
    snapshot = JSON.parse(await event.target.files[0].text());
    event.target.dataset.ready = "true";
  });
document.querySelector("#open").addEventListener("click", async () => {
  snapshot ||= await (
    await fetch(`/snapshots/${document.querySelector("#fixture").value}.json`)
  ).json();
  const child = window.open("/investigation/", "_blank");
  pending = { child, snapshot, deliveryId: crypto.randomUUID() };
});
window.addEventListener("message", (event) => {
  if (event.origin !== location.origin || event.source !== pending?.child)
    return;
  if (event.data?.type === "pbi-assure-viewer-snapshot-ready")
    pending.child.postMessage(
      {
        type: "pbi-assure-viewer-snapshot",
        snapshot: pending.snapshot,
        deliveryId: pending.deliveryId,
        workspace: document.querySelector("#workspace").value,
      },
      location.origin,
    );
  if (
    event.data?.type === "pbi-assure-viewer-snapshot-ack" &&
    event.data.deliveryId === pending.deliveryId
  ) {
    document.querySelector("#result").textContent = event.data.accepted
      ? "Accepted"
      : "Rejected";
    pending = null;
    snapshot = null;
  }
});
