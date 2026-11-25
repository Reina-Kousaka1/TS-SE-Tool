defmodule TsseRuntime.BootstrapTokenTest do
  use ExUnit.Case, async: false

  test "one-use bootstrap token cannot be replayed" do
    {:ok, server} = TsseRuntime.BootstrapToken.start_link(name: nil)
    token = TsseRuntime.BootstrapToken.issue_once(server)

    assert is_binary(token)
    assert TsseRuntime.BootstrapToken.consume(token, server) == :ok
    assert TsseRuntime.BootstrapToken.consume(token, server) == :error
    GenServer.stop(server)
  end

  test "wrong bootstrap token is rejected" do
    {:ok, server} = TsseRuntime.BootstrapToken.start_link(name: nil)
    _token = TsseRuntime.BootstrapToken.issue_once(server)

    assert TsseRuntime.BootstrapToken.consume("wrong-token", server) == :error
    GenServer.stop(server)
  end
end
