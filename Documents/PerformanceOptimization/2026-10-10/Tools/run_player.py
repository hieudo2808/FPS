import argparse
import ctypes
from ctypes import wintypes as W
import json
import pathlib
import shutil
import subprocess
import time
import uuid

parser = argparse.ArgumentParser()
parser.add_argument('exe', type=pathlib.Path)
parser.add_argument('output', type=pathlib.Path)
parser.add_argument('--scenarios', default='factory3,factory43,lab27,lab70,asylum10')
parser.add_argument('--variant', default='normal')
parser.add_argument('--repeats', type=int, default=3)
parser.add_argument('--seconds', type=int, default=60)
parser.add_argument('--warmup', type=int, default=30)
parser.add_argument('--profile', action='store_true')
parser.add_argument('--visual', action='store_true')
args = parser.parse_args()
args.output.mkdir(parents=True, exist_ok=True)
if any(args.output.iterdir()):
    raise RuntimeError('Output must be a new empty directory')

user = ctypes.WinDLL('user32', use_last_error=True)
kernel = ctypes.WinDLL('kernel32', use_last_error=True)
psapi = ctypes.WinDLL('psapi', use_last_error=True)
pdh = ctypes.WinDLL('pdh', use_last_error=True)
kernel.OpenProcess.argtypes = [W.DWORD, W.BOOL, W.DWORD]
kernel.OpenProcess.restype = W.HANDLE
kernel.CloseHandle.argtypes = [W.HANDLE]

class Memory(ctypes.Structure):
    _fields_ = [('cb', W.DWORD), ('pageFaults', W.DWORD)] + [(n, ctypes.c_size_t) for n in
        ['peakWorkingSet', 'workingSet', 'peakPaged', 'paged', 'peakNonPaged', 'nonPaged', 'pagefile', 'peakPagefile', 'private']]

class SystemMemory(ctypes.Structure):
    _fields_ = [('length', W.DWORD), ('load', W.DWORD)] + [(n, ctypes.c_ulonglong) for n in
        ['totalPhysical', 'availablePhysical', 'totalPagefile', 'availablePagefile', 'totalVirtual', 'availableVirtual', 'availableExtended']]

psapi.GetProcessMemoryInfo.argtypes = [W.HANDLE, ctypes.POINTER(Memory), W.DWORD]
kernel.GlobalMemoryStatusEx.argtypes = [ctypes.POINTER(SystemMemory)]
user.GetWindowThreadProcessId.argtypes = [W.HWND, ctypes.POINTER(W.DWORD)]
user.GetForegroundWindow.restype = W.HWND

class CounterValue(ctypes.Structure):
    _fields_ = [('status', W.DWORD), ('value', ctypes.c_longlong)]

class CounterItem(ctypes.Structure):
    _fields_ = [('name', W.LPWSTR), ('value', CounterValue)]

pdh.PdhOpenQueryW.argtypes = [W.LPCWSTR, ctypes.c_size_t, ctypes.POINTER(W.HANDLE)]
pdh.PdhAddEnglishCounterW.argtypes = [W.HANDLE, W.LPCWSTR, ctypes.c_size_t, ctypes.POINTER(W.HANDLE)]
pdh.PdhCollectQueryData.argtypes = [W.HANDLE]
pdh.PdhGetFormattedCounterArrayW.argtypes = [W.HANDLE, W.DWORD, ctypes.POINTER(W.DWORD), ctypes.POINTER(W.DWORD), ctypes.c_void_p]
pdh.PdhCloseQuery.argtypes = [W.HANDLE]
query, counter = W.HANDLE(), W.HANDLE()
gpu_available = pdh.PdhOpenQueryW(None, 0, ctypes.byref(query)) == 0 and pdh.PdhAddEnglishCounterW(
    query, r'\GPU Process Memory(*)\Dedicated Usage', 0, ctypes.byref(counter)) == 0

def gpu_memory(pid):
    if not gpu_available or pdh.PdhCollectQueryData(query) != 0:
        return None
    size, count = W.DWORD(), W.DWORD()
    pdh.PdhGetFormattedCounterArrayW(counter, 0x400, ctypes.byref(size), ctypes.byref(count), None)
    if size.value == 0:
        return None
    buffer = ctypes.create_string_buffer(size.value)
    if pdh.PdhGetFormattedCounterArrayW(counter, 0x400, ctypes.byref(size), ctypes.byref(count), buffer) != 0:
        return None
    items = ctypes.cast(buffer, ctypes.POINTER(CounterItem))
    return {items[i].name: items[i].value.value for i in range(count.value)
        if items[i].name.startswith(f'pid_{pid}_') and items[i].value.status in (0, 1)}

command = [str(args.exe.resolve()), '--fps-audit-output', str(args.output.resolve()),
    '--fps-audit-scenarios', args.scenarios, '--fps-audit-variant', args.variant,
    '--fps-audit-repeats', str(args.repeats), '--fps-audit-seconds', str(args.seconds),
    '--fps-audit-warmup', str(args.warmup), '-screen-width', '1920', '-screen-height', '1080',
    '-screen-fullscreen', '0', '-logFile', str((args.output/'Player.log').resolve())]
if args.profile: command.append('--fps-audit-profile')
if args.visual: command.append('--fps-audit-visual')
(args.output/'command.json').write_text(json.dumps(command), encoding='utf8')
presentmon = pathlib.Path(__file__).with_name('PresentMon-2.6.0-x64.exe')
trace = None
session_name = 'FPSOptimization' + uuid.uuid4().hex[:12]
trace_log = (args.output/'PresentMon.log').open('w', encoding='utf8')
if presentmon.exists():
    trace = subprocess.Popen([str(presentmon.resolve()), '--process_name', args.exe.name,
        '--output_file', str((args.output/'presentation.csv').resolve()), '--qpc_time',
        '--session_name', session_name, '--no_console_stats', '--set_circular_buffer_size', '65536',
        '--timed', str(300 + args.repeats * len(args.scenarios.split(',')) * (args.warmup + args.seconds + 10)),
        '--terminate_after_timed'], stdout=trace_log, stderr=subprocess.STDOUT,
        creationflags=subprocess.CREATE_NO_WINDOW)
    time.sleep(2)
process = subprocess.Popen(command)
(args.output/'runner.json').write_text(json.dumps({'pid': process.pid, 'sessionName': session_name}), encoding='utf8')
handle = kernel.OpenProcess(0x410, False, process.pid)
gpu_log = (args.output/'gpu.csv').open('w', encoding='utf8')
gpu_telemetry = None
if shutil.which('nvidia-smi'):
    gpu_telemetry = subprocess.Popen(['nvidia-smi', '--loop-ms=2000',
        '--query-gpu=timestamp,temperature.gpu,clocks.sm,clocks.mem,power.draw,memory.used,memory.total,utilization.gpu',
        '--format=csv,noheader,nounits'], stdout=gpu_log, stderr=subprocess.STDOUT,
        creationflags=subprocess.CREATE_NO_WINDOW)
print(json.dumps({'pid': process.pid, 'output': str(args.output), 'gpuCounter': gpu_available}), flush=True)
seen, start = set(), time.monotonic()
next_metrics = 0
last_foreground = None

try:
    with (args.output/'system.jsonl').open('w', encoding='utf8') as metrics, (args.output/'focus.jsonl').open('w', encoding='utf8') as focus:
        while process.poll() is None:
            if time.monotonic() - start > 7200:
                process.terminate()
                raise TimeoutError('Audit player timeout')
            foreground = user.GetForegroundWindow()
            if foreground != last_foreground:
                foreground_pid = W.DWORD()
                user.GetWindowThreadProcessId(foreground, ctypes.byref(foreground_pid))
                focus.write(json.dumps({'qpcSeconds': time.perf_counter(), 'foregroundPid': foreground_pid.value,
                    'playerForeground': foreground_pid.value == process.pid}) + '\n')
                focus.flush()
                last_foreground = foreground
            if time.monotonic() < next_metrics:
                time.sleep(.1)
                continue
            next_metrics = time.monotonic() + 2
            memory, system = Memory(), SystemMemory()
            memory.cb = ctypes.sizeof(memory)
            system.length = ctypes.sizeof(system)
            process_memory_ok = psapi.GetProcessMemoryInfo(handle, ctypes.byref(memory), memory.cb)
            system_memory_ok = kernel.GlobalMemoryStatusEx(ctypes.byref(system))
            metrics.write(json.dumps({'elapsed': time.monotonic()-start, 'qpcSeconds': time.perf_counter(),
                'privateBytes': memory.private if process_memory_ok else None,
                'workingSetBytes': memory.workingSet if process_memory_ok else None,
                'pageFaults': memory.pageFaults if process_memory_ok else None,
                'availablePhysicalBytes': system.availablePhysical if system_memory_ok else None,
                'systemMemoryLoad': system.load if system_memory_ok else None,
                'gpuDedicatedInstances': gpu_memory(process.pid)})+'\n')
            metrics.flush()
            for file in args.output.glob('*.txt'):
                if file.name not in seen:
                    seen.add(file.name)
                    print(file.name+': '+file.read_text(encoding='utf-8-sig')[:450].replace('\n', ' | '), flush=True)
            time.sleep(.1)
finally:
    if handle: kernel.CloseHandle(handle)
    if query: pdh.PdhCloseQuery(query)
    if trace:
        subprocess.run([str(presentmon.resolve()), '--session_name', session_name,
            '--terminate_existing_session'], stdout=trace_log, stderr=subprocess.STDOUT,
            timeout=15, creationflags=subprocess.CREATE_NO_WINDOW)
        try: trace.wait(timeout=15)
        except subprocess.TimeoutExpired:
            trace.terminate()
            trace.wait(timeout=5)
    trace_log.close()
    if gpu_telemetry:
        gpu_telemetry.terminate()
        gpu_telemetry.wait(timeout=5)
    gpu_log.close()
print(json.dumps({'exitCode': process.returncode, 'complete': (args.output/'complete.txt').exists(),
    'failure': (args.output/'failed.txt').read_text() if (args.output/'failed.txt').exists() else None}), flush=True)
if process.returncode != 0 or not (args.output/'complete.txt').exists():
    raise SystemExit(1)
