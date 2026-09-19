"""Invoke a configured local MCP tool without exposing transport credentials."""
import json
import os
from pathlib import Path
import subprocess
import sys
import requests

ROOT = Path(__file__).resolve().parents[3]


def invoke(server, tool, arguments):
    config = json.loads((ROOT / '.mcp.json').read_text())["mcpServers"][server]
    process = None
    session = None
    if config.get('type') == 'stdio':
        process = subprocess.Popen(
            [config['command'], *config.get('args', [])], cwd=ROOT,
            env={**os.environ, **config.get('env', {})}, stdin=subprocess.PIPE,
            stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True, encoding='utf-8')
    else:
        session = requests.Session()
        session.headers.update(config.get('headers', {}))
        session.headers['Accept'] = 'application/json, text/event-stream'

    def call(identifier, method, params):
        payload = {'jsonrpc': '2.0', 'id': identifier, 'method': method, 'params': params}
        if process:
            process.stdin.write(json.dumps(payload) + '\n')
            process.stdin.flush()
            while True:
                line = process.stdout.readline()
                if not line:
                    raise RuntimeError('MCP server exited')
                response = json.loads(line)
                if response.get('id') == identifier:
                    return response
        response = session.post(config['url'], json=payload, timeout=180)
        response.raise_for_status()
        if response.headers.get('Mcp-Session-Id'):
            session.headers['Mcp-Session-Id'] = response.headers['Mcp-Session-Id']
        raw = response.text
        if raw.startswith(('event:', 'data:')):
            raw = next(line[6:] for line in raw.splitlines() if line.startswith('data: '))
        return json.loads(raw)

    try:
        call(1, 'initialize', {'protocolVersion': '2024-11-05', 'capabilities': {},
                              'clientInfo': {'name': 'codex-ui', 'version': '1'}})
        return call(2, 'tools/call', {'name': tool, 'arguments': arguments})
    finally:
        if process:
            process.terminate()
        if session:
            session.close()


if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    arguments = json.loads(sys.stdin.read())
    print(json.dumps(invoke(sys.argv[1], sys.argv[2], arguments), ensure_ascii=False))
