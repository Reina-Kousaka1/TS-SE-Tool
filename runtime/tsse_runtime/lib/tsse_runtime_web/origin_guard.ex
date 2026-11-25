defmodule TsseRuntimeWeb.OriginGuard do
  import Plug.Conn

  def init(opts), do: opts

  def call(conn, _opts) do
    port = Application.fetch_env!(:tsse_runtime, :runtime_port)
    origin = conn |> get_req_header("origin") |> List.first()

    cond do
      conn.host != "127.0.0.1" or conn.port != port or not loopback?(conn.remote_ip) ->
        reject(conn)

      is_nil(origin) ->
        conn

      exact_origin?(origin, port) ->
        conn

      true ->
        reject(conn)
    end
  end

  def websocket_origin?(%URI{scheme: "http", host: "127.0.0.1", port: port}, expected_port),
    do: port == expected_port

  def websocket_origin?(_origin, _expected_port), do: false

  defp exact_origin?(origin, port), do: origin == "http://127.0.0.1:#{port}"
  defp loopback?({127, _, _, _}), do: true
  defp loopback?(_), do: false

  defp reject(conn) do
    conn
    |> send_resp(:forbidden, "local origin required")
    |> halt()
  end
end
