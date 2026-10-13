import ctypes
import json
import pathlib

class Memory(ctypes.Structure):
    _fields_ = [('length', ctypes.c_ulong), ('load', ctypes.c_ulong),
                ('totalPhys', ctypes.c_ulonglong), ('availPhys', ctypes.c_ulonglong),
                ('totalPage', ctypes.c_ulonglong), ('availPage', ctypes.c_ulonglong),
                ('totalVirtual', ctypes.c_ulonglong), ('availVirtual', ctypes.c_ulonglong),
                ('availExtended', ctypes.c_ulonglong)]

memory = Memory()
memory.length = ctypes.sizeof(memory)
if not ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(memory)):
    raise ctypes.WinError()
result = {'ramLoadPercent': memory.load, 'availableRAM_MB': memory.availPhys // 1048576,
          'availableCommit_MB': memory.availPage // 1048576}
print(json.dumps(result))
