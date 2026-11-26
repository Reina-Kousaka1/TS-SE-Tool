import os
import subprocess
import sys


app_path = os.environ.get("TSSE_APP_PATH")
if not app_path:
    sys.exit("TSSE_APP_PATH must identify the desktop executable.")

env = os.environ.copy()
env["TSSE_DESKTOP_CI_SMOKE_TEST"] = "1"
process = subprocess.Popen([app_path], env=env)
try:
    exit_code = process.wait(timeout=25)
except subprocess.TimeoutExpired:
    process.terminate()
    try:
        process.wait(timeout=5)
    except subprocess.TimeoutExpired:
        process.kill()
        process.wait()
    sys.exit("The Avalonia desktop process did not close after its window startup smoke test.")

if exit_code != 0:
    sys.exit(f"The Avalonia desktop process exited with code {exit_code}.")
print("The Avalonia desktop process opened and closed cleanly.")
