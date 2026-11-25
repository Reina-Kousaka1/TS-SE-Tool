defmodule TsseRuntimeWeb.BootstrapController do
  use Phoenix.Controller, formats: [:html, :json]
  import Plug.Conn

  def index(conn, _params) do
    csrf =
      Plug.CSRFProtection.get_csrf_token()
      |> Phoenix.HTML.html_escape()
      |> Phoenix.HTML.safe_to_string()

    html = """
    <!doctype html>
    <html lang="en">
      <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <meta name="csrf-token" content="#{csrf}">
        <title>TS-SE-Tool local foundation</title>
        <script type="module" src="/assets/app.js"></script>
      </head>
      <body>
        <main>
          <h1>TS-SE-Tool local foundation</h1>
          <p id="startup-status">Waiting for a one-use local launch token.</p>
        </main>
      </body>
    </html>
    """

    conn
    |> put_resp_content_type("text/html")
    |> send_resp(:ok, html)
  end

  def create(conn, _params) do
    token = conn |> get_req_header("x-tsse-bootstrap-token") |> List.first()
    origin = conn |> get_req_header("origin") |> List.first()
    port = Application.fetch_env!(:tsse_runtime, :runtime_port)

    if origin == "http://127.0.0.1:#{port}" and TsseRuntime.BootstrapToken.consume(token) == :ok do
      Plug.CSRFProtection.delete_csrf_token()

      conn
      |> configure_session(renew: true)
      |> put_session(:authenticated, true)
      |> json(%{redirect: "/dashboard"})
    else
      conn
      |> put_status(:unauthorized)
      |> json(%{error: "bootstrap_rejected"})
    end
  end
end
