defmodule TsseRuntimeWeb.OriginGuardTest do
  use ExUnit.Case, async: true
  use Plug.Test

  alias TsseRuntimeWeb.OriginGuard

  test "accepts only a loopback host and exact local origin" do
    port = Application.fetch_env!(:tsse_runtime, :runtime_port)

    conn =
      conn(:get, "/")
      |> Map.put(:host, "127.0.0.1")
      |> Map.put(:port, port)
      |> put_req_header("origin", "http://127.0.0.1:#{port}")

    assert OriginGuard.call(conn, []).halted == false
  end

  test "rejects foreign origins and hostnames" do
    port = Application.fetch_env!(:tsse_runtime, :runtime_port)

    foreign_origin =
      conn(:get, "/")
      |> Map.put(:host, "127.0.0.1")
      |> Map.put(:port, port)
      |> put_req_header("origin", "http://attacker.example")
      |> OriginGuard.call([])

    foreign_host =
      conn(:get, "/")
      |> Map.put(:host, "localhost")
      |> Map.put(:port, port)
      |> OriginGuard.call([])

    assert foreign_origin.status == 403
    assert foreign_origin.halted
    assert foreign_host.status == 403
    assert foreign_host.halted
  end

  test "websocket origin check requires the configured IPv4 origin and port" do
    port = Application.fetch_env!(:tsse_runtime, :runtime_port)

    assert OriginGuard.websocket_origin?(URI.parse("http://127.0.0.1:#{port}"), port)
    refute OriginGuard.websocket_origin?(URI.parse("http://localhost:#{port}"), port)
    refute OriginGuard.websocket_origin?(URI.parse("http://127.0.0.1:#{port + 1}"), port)
  end
end
