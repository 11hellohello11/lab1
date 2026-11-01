let current;
const id = () => encodeURIComponent(document.querySelector("#incident-id").value.trim());
const status = document.querySelector("#status");
async function load() {
  const response = await fetch(`/api/incidents/${id()}`);
  status.textContent = `GET: ${response.status}`;
  if (!response.ok) return;
  current = await response.json();
  document.querySelector("#title").textContent = current.title;
  document.querySelector("#description").textContent = current.description;
  document.querySelector("#comments").replaceChildren();
  for (const comment of current.comments ?? []) {
    const paragraph = document.createElement("p");
     paragraph.textContent = comment.text;
    document.querySelector("#comments").append(paragraph);
  }
  document.querySelector("#edit").elements.description.value = current.description;
}
document.querySelector("#load").addEventListener("click", load);
document.querySelector("#edit").addEventListener("submit", async event => {
    event.preventDefault();
    const form = event.currentTarget;
    const tokenResponse = await fetch("/api/antiforgery/token");

    if (!tokenResponse.ok) {
        status.textContent = `CSRF token: ${tokenResponse.status}`;
        return;
    }
    const { token } = await tokenResponse.json();
    const response = await fetch("/api/incidents", {
        method: "POST",
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": token },
        body: JSON.stringify({
            title: form.elements.title.value, description: form.elements.description.value,
      severity: "Low", occurredAtUtc: new Date().toISOString() }) });
  status.textContent = `POST: ${response.status}`;
  if (response.ok) {
    const created = await response.json();
    document.querySelector("#incident-id").value = created.id;
    await load();
  }
});
