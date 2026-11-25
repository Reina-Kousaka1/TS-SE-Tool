defmodule TsseRuntime.Bridge.ClientIntegrationTest do
  use ExUnit.Case, async: false

  @moduletag skip: is_nil(System.get_env("TSSE_BRIDGE_EXECUTABLE"))

  test "the OTP port starts the real .NET bridge and returns its read-only status" do
    assert {:ok, response} = TsseRuntime.Bridge.Client.status()
    assert response["status"] == "ok"
    assert response["engine"]["processState"] == "foundation-only"
    assert response["engine"]["verifiedGameAdapterCount"] == 0
    assert response["engine"]["saveWriteEnabled"] == false
  end
end
