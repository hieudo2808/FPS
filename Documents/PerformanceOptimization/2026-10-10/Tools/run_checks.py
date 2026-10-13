"""Run existing focused regressions, with the project's authored-scene Save Gate."""
import json
import pathlib
import sys
import time

sys.path.insert(0, '.codex/performance-audit')
import unity

output = pathlib.Path('Documents/PerformanceOptimization/2026-10-10/Regression')
output.mkdir(exist_ok=True)
checks = [
    ('EditMode', 'FPS.Tests.HudRegressionTests.ProjectionHandlesBehindCameraAndAvoidsOccupiedCorner'),
    ('EditMode', 'FPS.Tests.HelicopterRappelTests.ImportedRopesAndFourActorsFollowTheFasterDescentWhenSeeking'),
    ('EditMode', 'FPS.Tests.SettingsDifficultyRegressionTests'),
    ('PlayMode', 'FPS.Tests.CampaignLifecycleTests'),
    ('PlayMode', 'FPS.Tests.HudGameplayAcceptanceTests.NavigationStaysActiveAndInsertionRestoresPlayerCameras'),
]

def call(name, body=None):
    try:
        envelope = unity.call(name, body)
    except OSError:
        if name not in ('test_get_result', 'editor_get_context', 'scene_get_loaded'):
            raise
        unity.ready()
        envelope = unity.call(name, body)
    result = envelope.get('result', envelope)
    assert result.get('success', True), envelope
    return result

def save():
    call('editor_execute_menu', {'menuPath': 'Tools/FPS/UI/Save Authored Scenes'})

unity.ready()
for mode, name in checks:
    result_file = output/(name + '.json')
    if result_file.exists() and json.loads(result_file.read_text()).get('status') == 'completed':
        continue
    active = output/'active-check.json'
    if active.exists():
        started = json.loads(active.read_text())
        assert started['testName'] == name, 'Resume the active test before starting another'
    else:
        save()
        started = call('test_run_by_name', {'testMode': mode, 'testName': name})
        active.write_text(json.dumps(started), encoding='utf8')
    print(json.dumps({'test': name, 'started': started}), flush=True)
    deadline = time.monotonic() + 420
    while time.monotonic() < deadline:
        time.sleep(2)
        result = call('test_get_result', {'jobId': started['jobId']})
        if result.get('status', '').lower() in ('completed', 'failed', 'cancelled'):
            break
    else:
        raise TimeoutError(name)
    assert result.get('totalTests', 0) > 0, result
    result_file.write_text(json.dumps(result, indent=2), encoding='utf8')
    print(json.dumps({'test': name, 'result': result}), flush=True)
    unity.ready()
    for _ in range(30):
        if not call('editor_get_context')['isPlaying']:
            break
        time.sleep(1)
    else:
        raise RuntimeError('Test Runner has not exited Play Mode; do not save runtime state')
    save()
    active.unlink()
    (output/(name + '-scenes.json')).write_text(json.dumps(call('scene_get_loaded'), indent=2), encoding='utf8')
