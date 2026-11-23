const fs = require("node:fs");
const { spawnSync } = require("node:child_process");

const packet = JSON.parse(fs.readFileSync(0, "utf8"));
fs.writeFileSync("/tmp/player.cjs", packet.source);

const result = spawnSync(process.execPath, ["/tmp/player.cjs"], {
    input: JSON.stringify(packet.turn),
    stdio: ["pipe", "inherit", "inherit"]
});
process.exit(result.status === 0 ? 0 : 1);
