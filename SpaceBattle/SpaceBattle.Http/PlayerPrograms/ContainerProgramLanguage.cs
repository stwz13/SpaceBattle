using SpaceBattle.Lib;

namespace SpaceBattle.Http;

internal sealed record ContainerProgramLanguage(string Name, IPlayerProgramSandbox Sandbox,
    ProgramContainerLaunch Launch) : IPlayerProgramLanguage
{
    public Task<string> Run(string source, ProgramTurn turn, CancellationToken cancellationToken) =>
        Sandbox.Run(Launch, source, turn, cancellationToken);

    public static IPlayerProgramLanguage Python(ContainerLanguageSettings settings, IPlayerProgramSandbox sandbox) =>
        new ContainerProgramLanguage(settings.Name, sandbox, new ProgramContainerLaunch(settings.Image, settings.EntryPoint,
        ["-I", "-B", "-c", """
        import json, subprocess, sys
        packet = json.load(sys.stdin)
        with open('/tmp/player.py', 'w', encoding='utf-8') as program:
            program.write(packet['source'])
        result = subprocess.run([sys.executable, '-I', '-B', '/tmp/player.py'],
                                input=json.dumps(packet['turn']).encode('utf-8'))
        sys.exit(0 if result.returncode == 0 else 1)
        """]));

    public static IPlayerProgramLanguage JavaScript(ContainerLanguageSettings settings, IPlayerProgramSandbox sandbox) =>
        new ContainerProgramLanguage(settings.Name, sandbox, new ProgramContainerLaunch(settings.Image, settings.EntryPoint,
        ["-e", """
        const fs = require('node:fs');
        const { spawnSync } = require('node:child_process');
        const packet = JSON.parse(fs.readFileSync(0, 'utf8'));
        fs.writeFileSync('/tmp/player.cjs', packet.source);
        const result = spawnSync(process.execPath, ['/tmp/player.cjs'], {
            input: JSON.stringify(packet.turn), stdio: ['pipe', 'inherit', 'inherit']
        });
        process.exit(result.status === 0 ? 0 : 1);
        """]));
}
