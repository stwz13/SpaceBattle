import json
import subprocess
import sys

packet = json.load(sys.stdin)
with open("/tmp/player.py", "w", encoding="utf-8") as program:
    program.write(packet["source"])

result = subprocess.run(
    [sys.executable, "-I", "-B", "/tmp/player.py"],
    input=json.dumps(packet["turn"]).encode("utf-8"),
)
sys.exit(0 if result.returncode == 0 else 1)
