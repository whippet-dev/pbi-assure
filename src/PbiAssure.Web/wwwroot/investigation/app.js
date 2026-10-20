import { receiveAnalysis, returnToProject } from "./hosted-entry.js";
import { escape } from "./ui.js";
let delivery;
try {
  delivery = await receiveAnalysis();
  const { mountSnapshot } = await import("./controller.js");
  mountSnapshot(delivery.snapshot, { workspace: delivery.workspace });
  delivery.acknowledge(true);
} catch (error) {
  delivery?.acknowledge(false);
  document.querySelector("#app").innerHTML =
    '<div class="empty"><h1>Could not open the investigation</h1><p>' +
    escape(error.message) +
    '</p><p><a href="../" data-project-return>Return to project</a></p></div>';
}
document.addEventListener("click", (event) => {
  if (event.target.closest("[data-project-return]")) returnToProject(event);
});
