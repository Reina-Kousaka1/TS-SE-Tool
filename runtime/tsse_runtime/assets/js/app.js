import { Socket } from "phoenix";
import { LiveSocket } from "phoenix_live_view";

const status = document.getElementById("startup-status");
const csrfToken = document.querySelector('meta[name="csrf-token"]')?.content;
const bootstrapToken = window.location.hash.slice(1);

if (window.location.hash) {
  window.history.replaceState(null, "", window.location.pathname + window.location.search);
}

async function startLocalSession() {
  if (!csrfToken || !bootstrapToken) {
    return false;
  }

  try {
    const response = await fetch("/bootstrap", {
      method: "POST",
      credentials: "same-origin",
      headers: {
        "content-type": "application/json",
        "x-csrf-token": csrfToken,
        "x-tsse-bootstrap-token": bootstrapToken,
      },
      body: "{}",
    });

    if (!response.ok) {
      return false;
    }

    const result = await response.json();
    window.location.replace(result.redirect);
    return true;
  } catch (_error) {
    return false;
  }
}

const liveSocket = new LiveSocket("/live", Socket, {
  params: { _csrf_token: csrfToken },
  reconnectAfterMs: (tries) => Math.min(1000 * 2 ** tries, 10_000),
});

async function main() {
  const authenticated = await startLocalSession();
  if (!authenticated && status) {
    status.textContent = "No valid one-use launch token. Close this window and start the local desktop runtime again.";
  }

  if (bootstrapToken || document.getElementById("engine-status")) {
    liveSocket.connect();
  }
}

void main();
