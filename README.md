# MarcasZK

**Attendance mark extraction from ZKTeco biometric devices with DDD architecture, COM interop, and Oracle-format export.**

.NET Framework 4.8 | C# 7.3 | x64 | xUnit + Moq + FluentAssertions

---

## What This Project Does

MarcasZK connects to ZKTeco fingerprint time clocks over TCP/IP (via VPN), downloads attendance logs through the zkemkeeper COM/ActiveX SDK, and exports each mark as an individual Oracle-format `.txt` file. It optionally purges device logs after a confirmed successful export.

The entire codebase follows **Domain-Driven Design** with strict layer separation, dependency inversion through port interfaces, and a composition root that wires everything with zero DI container.

---

## Architecture — DDD Layers

```
MarcasZK/
├── Program.cs                                  # Composition Root
├── config.json                                 # Device configuration
│
├── Domain/                                     # Pure domain — zero infrastructure dependencies
│   ├── AttendanceMark.cs                       # Value Object
│   ├── Device.cs                               # Entity
│   ├── DeviceConnection.cs                     # Value Object
│   ├── ReadMarksResult.cs                      # Result type + ReadErrorType enum
│   ├── ClearMarksResult.cs                     # Result type
│   ├── IDeviceReader.cs                        # Port — reads marks from a device
│   ├── IMarkExporter.cs                        # Port — persists a single mark
│   └── IDeviceCleaner.cs                       # Port — clears device logs
│
├── Application/                                # Use case orchestration — depends only on Domain
│   ├── ExportAttendanceMarksService.cs         # Use case — export + optional delete
│   └── ILogger.cs                              # Port — application-level logging
│
├── Infrastructure/                             # Adapters — implements Domain + Application ports
│   ├── ZktecoDeviceReader.cs                   # IDeviceReader via zkemkeeper COM interop
│   ├── ZktecoDeviceCleaner.cs                  # IDeviceCleaner via zkemkeeper COM interop
│   ├── TxtMarkExporter.cs                      # IMarkExporter via File.WriteAllText
│   ├── FileLogger.cs                           # ILogger — dual output (file + console)
│   └── JsonConfigLoader.cs                     # Config reader — JavaScriptSerializer, no NuGet
│
└── MarcasZK.Tests/                             # Unit tests — 47 tests, 51 scenarios
    ├── Domain/
    │   └── ReadMarksResultTests.cs             # Constructor defaults, enum completeness
    ├── Application/
    │   └── ExportAttendanceMarksServiceTests.cs # 27 tests — full use case coverage with Moq
    └── Infrastructure/
        └── JsonConfigLoaderTests.cs            # 16 tests — all parse paths and edge cases
```

### Dependency Flow

```
Domain  ←──  Application  ←──  Infrastructure
  (ports)      (use cases)       (adapters)
                    ↑
              Program.cs (Composition Root)
```

**Domain** defines contracts. **Application** orchestrates through those contracts. **Infrastructure** implements them. **Program.cs** wires implementations to interfaces — the only place in the codebase where concrete infrastructure types are instantiated.

---

## Domain Layer — Pure, No Dependencies

The domain has **zero `using` statements** for `System.IO`, `System.Net`, or any SDK namespace. It defines the business language through value objects, entities, and port interfaces.

### Port Interfaces (Dependency Inversion)

```csharp
namespace MarcasZK.Domain
{
    public interface IDeviceReader
    {
        ReadMarksResult ReadMarks(Device device);
    }

    public interface IMarkExporter
    {
        void Export(AttendanceMark mark, string outputDirectory);
    }

    public interface IDeviceCleaner
    {
        ClearMarksResult ClearMarks(Device device);
    }
}
```

Every infrastructure concern — COM interop, file system, network — lives behind these interfaces. The domain never knows *how* marks are read or exported, only *what* the contracts are.

### Result Types (No Exceptions for Expected Failures)

```csharp
public class ReadMarksResult
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; }
    public int ErrorCode { get; set; }
    public ReadErrorType ErrorType { get; set; }
    public List<AttendanceMark> Marks { get; set; }

    public ReadMarksResult()
    {
        Marks = new List<AttendanceMark>();
        ErrorType = ReadErrorType.None;
    }
}

public enum ReadErrorType
{
    None, Connection, ReadData, NoData, ComNotRegistered, Unexpected
}
```

Connection failures and SDK errors are **expected outcomes**, not exceptions. The `ReadErrorType` enum classifies failures so the application layer can provide structured, translatable error messages.

---

## Application Layer — Use Case Orchestration

One class, one responsibility: iterate devices, read marks, export them, optionally clear device logs.

```csharp
public class ExportAttendanceMarksService
{
    private readonly IDeviceReader _reader;
    private readonly IMarkExporter _exporter;
    private readonly ILogger _logger;
    private readonly IDeviceCleaner _cleaner;

    public ExportAttendanceMarksService(
        IDeviceReader reader, IMarkExporter exporter, ILogger logger,
        IDeviceCleaner cleaner = null) { /* ... */ }

    public int Execute(Device[] devices, string outputDirectory) { /* ... */ }
}
```

Key design decisions in the use case:

- **Per-device isolation**: one device's failure never blocks the next
- **4-guard delete chain**: `exportSucceeded && deviceCount > 0 && device.BorrarMarcas && _cleaner != null` — delete only runs after a confirmed full export
- **Inner try/catch on export loop**: `exportSucceeded` flag is only set to `true` after the entire foreach completes, preventing partial-export deletes
- **Error code translation**: SDK numeric codes mapped to human-readable Spanish descriptions
- **Optional `IDeviceCleaner`**: nullable constructor parameter — backward-compatible, no NullObject pattern needed

---

## Infrastructure Layer — Adapters

### ZktecoDeviceReader (COM Interop)

Implements `IDeviceReader` through the zkemkeeper ActiveX SDK:

```
CZKEMClass → SetCommPasswordEx → Connect_Net → EnableDevice(false)
    → ReadGeneralLogData → SSR_GetGeneralLogData loop
    → EnableDevice(true) → Disconnect (finally-guaranteed)
```

- `EnableDevice(false)` before download is critical for data consistency
- `GetLastError` captures SDK error codes on every failure path
- `COMException` caught separately from generic exceptions
- `EnableDevice(true)` + `Disconnect` run in `finally` — device is never left locked

### ZktecoDeviceCleaner (Separate Connection)

Opens its **own TCP connection** for the delete operation. The reader already disconnects in `finally` (sealed contract). A second short-lived connection avoids architectural changes to `IDeviceReader`.

```
Connect_Net → EnableDevice(false) → ClearGLog → RefreshData (only on success)
    → EnableDevice(true) → Disconnect (finally-guaranteed)
```

`RefreshData` after `ClearGLog` is required — without it the device stays in an inconsistent state. This is non-obvious from the SDK documentation and was discovered through the SDK demo code analysis.

### TxtMarkExporter (Oracle Format)

Each mark produces one file: `{origen}-{yyyyMMddHHmmss}-{employeeId}.txt`

Content follows Oracle `TO_DATE` import format:
```
'HF','12345', TO_DATE('11/07/2026 08:30:00','DD/MM/YYYY HH24:MI:SS'),'-'
```

### FileLogger (Dual Output)

Writes every log line to both `Console` and a daily file (`logs/yyyyMMdd.txt`). Uses `File.AppendAllText` — atomic, stateless, no `Dispose` lifecycle. Date resolution happens per-write to handle midnight crossings correctly.

---

## Composition Root

```csharp
class Program
{
    static int Main(string[] args)
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            ILogger logger = new FileLogger(Path.Combine(baseDir, "logs"));
            logger.Log("Inicio Version: " + Assembly.GetExecutingAssembly()
                .GetName().Version.ToString());

            Device[] devices = JsonConfigLoader.Load(Path.Combine(baseDir, "config.json"));
            logger.Log(string.Format("Cantidad de dispositivos: {0}", devices.Length));

            Directory.CreateDirectory(Path.Combine(baseDir, "marcas"));

            IDeviceReader reader   = new ZktecoDeviceReader();
            IMarkExporter exporter = new TxtMarkExporter();
            IDeviceCleaner cleaner = new ZktecoDeviceCleaner();

            var service = new ExportAttendanceMarksService(reader, exporter, logger, cleaner);
            int total = service.Execute(devices, Path.Combine(baseDir, "marcas"));

            logger.Log(string.Format("Total marcas exportadas: {0}", total));
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Fatal: {0}", ex.Message);
            return 1;
        }
    }
}
```

Manual dependency injection. No container. Every concrete type is instantiated **here and only here**. The fatal `catch` uses `Console.WriteLine` directly — if `FileLogger` creation itself fails, the logger is unavailable by design.

---

## Test Suite

**47 tests | 51 spec scenarios | 100% spec coverage**

| Layer | Tests | What's Covered |
|-------|-------|----------------|
| Domain | 4 | Constructor defaults, `ReadErrorType` enum completeness |
| Application | 27 | Connection failures, zero marks, successful export, export exceptions, clear-after-export, multiple devices, error-code translation |
| Infrastructure | 16 | Valid JSON (1 and 3 devices), missing required fields, type errors, optional fields, malformed JSON, empty/null input |

### Stack

| Package | Version | Purpose |
|---------|---------|---------|
| xUnit | 2.9.3 | Test framework |
| Moq | 4.20.72 | Interface mocking |
| FluentAssertions | 6.12.2 | Readable assertions (pinned to 6.x — 7.x dropped .NET Framework support) |
| Microsoft.NET.Test.Sdk | 17.8.0 | Test host |

### Build and Run

```powershell
# Build (requires VS 2022 MSBuild — dotnet build does NOT support COM references)
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe" `
    MarcasZK.Tests.csproj /p:Platform=x64 /p:Configuration=Debug

# Run tests
& "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe" `
    bin\x64\Debug\net48\MarcasZK.Tests.dll /Platform:x64
```

> **Why not `dotnet test`?** The main project has a `<COMReference>` for zkemkeeper. The .NET Core MSBuild (`dotnet build`) does not support `ResolveComReference` (fails with MSB4803). The test project itself is SDK-style with PackageReference, but it references the COM-dependent main project, so the full VS 2022 MSBuild toolchain is required.

### Testing Design

COM interop is **never loaded at test time**. All COM-backed adapters (`ZktecoDeviceReader`, `ZktecoDeviceCleaner`) are mocked behind `IDeviceReader` and `IDeviceCleaner`. No SDK registration is required on the test machine.

The only production code change for testability was extracting `JsonConfigLoader.LoadFromJson(string json)` — a public static method that receives raw JSON instead of a file path. The original `Load(string path)` became a 3-line wrapper: check file exists, read text, call `LoadFromJson`. This is the **minimal seam** pattern: if a static method mixes I/O + logic, extract the logic into a testable overload.

---

## Development Workflow — Spec-Driven Development (SDD)

Every feature was built through a structured SDD cycle using [OpenCode](https://opencode.ai/) + [Engram](https://github.com/nicobailon/engram) persistent memory:

```
Explore → Propose → Spec + Design → Tasks → Apply → Verify → Archive
```

### Completed SDD Cycles

| # | Change | Files Created | Files Modified | Lines | Verified |
|---|--------|--------------|----------------|-------|----------|
| 1 | `marcaszk-config-export` | 11 | 1 | ~400 | 24/26 tasks, 0 errors |
| 2 | `marcaszk-logging` | 2 | 5 | ~180 | 7/7 tasks, 0 errors |
| 3 | `borrar-marcas` | 3 | 4 | ~136 | 21/21 scenarios, 0 errors |
| 4 | `marcaszk-unit-tests` | 4 | 2 | ~900 | 47/47 tests, 51/51 scenarios |

### What Each Phase Produces

| Phase | Purpose | Output |
|-------|---------|--------|
| **Explore** | Investigate the codebase and SDK; understand constraints | Exploration report with current state, risks, and approach options |
| **Propose** | Define intent, scope, and high-level approach | Proposal with boundaries, risks, and what's explicitly out of scope |
| **Spec** | Write delta specs — requirements and acceptance scenarios | Numbered scenarios with given/when/then conditions |
| **Design** | Technical architecture, data flow diagrams, interface contracts | Design document with folder structure, file changes, and code contracts |
| **Tasks** | Break specs into implementation tasks with workload forecast | Ordered task list with line estimates and PR risk assessment |
| **Apply** | Implement tasks in batches, track progress | Working code + apply-progress report with per-task status |
| **Verify** | Validate implementation against specs | Verification report: pass/fail per scenario, build status, warnings |
| **Archive** | Close the change, record lessons learned | Archive report with deviations, lessons, and artifact traceability |

### Toolchain

| Tool | Role |
|------|------|
| **OpenCode** | AI-assisted development orchestrator — delegates SDD phases to specialized sub-agents |
| **Engram** | Persistent memory across sessions — every SDD artifact is stored with topic keys (`sdd/{change}/explore`, `sdd/{change}/proposal`, etc.) |
| **Git** | Version control with conventional commits |
| **VS 2022 MSBuild** | Build toolchain (required for COM interop) |
| **vstest.console.exe** | Test runner |

### Artifact Traceability

Every SDD artifact is persisted in Engram with a consistent topic key format:

```
sdd/{change-name}/explore
sdd/{change-name}/proposal
sdd/{change-name}/spec
sdd/{change-name}/design
sdd/{change-name}/tasks
sdd/{change-name}/apply-progress
sdd/{change-name}/verify-report
sdd/{change-name}/archive-report
```

Any future session can query `mem_search("sdd/borrar-marcas/design")` and recover the full architectural reasoning, interface contracts, and data flow diagrams that drove the implementation.

---

## Key Technical Decisions

| Decision | Choice | Why |
|----------|--------|-----|
| Single assembly with folder namespaces | Over multi-project solution | Business constraint — DDD separation via folders without build complexity |
| Manual DI in Program.cs | Over DI container | Console app, 4 dependencies — a container would be over-engineering |
| Result types over exceptions | For expected failures | Connection/SDK errors are normal flow, not exceptional conditions |
| Separate `IDeviceCleaner` port | Over extending `IDeviceReader` | Keeps reader contract sealed; clean port/adapter symmetry |
| Two TCP connections for read+delete | Over keeping connection alive | Reader disconnects in `finally` (sealed). Second connection is simpler and safer |
| `File.AppendAllText` for logging | Over `StreamWriter` | Atomic, stateless — no lifecycle management for a sequential single-run app |
| `JavaScriptSerializer` for JSON | Over Newtonsoft/System.Text.Json | Ships with .NET Framework 4.8 (`System.Web.Extensions`) — zero NuGet dependencies |
| `FluentAssertions` pinned to 6.x | Over latest 7.x | 7.0+ dropped .NET Framework support — builds fail with confusing type-resolution errors |
| SDK-style .csproj for tests | Over old-style with packages.config | `nuget.exe` CLI was not available; SDK-style supports `dotnet restore` + VS MSBuild |
| `LoadFromJson` extraction | Over `IFileSystem` abstraction | Minimal seam — one static method unlocks all 16 config parse test scenarios |

---

## Configuration

```json
{
  "Devices": [
    {
      "ip": "172.0.0.1",
      "port": 4370,
      "password": "ABC123",
      "origen": "external-Id",
      "nombre": "Reloj Principal",
      "borrarMarcas": false
    }
  ]
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `ip` | Yes | Device IP address |
| `port` | Yes | TCP port (default ZKTeco: 4370, uses UDP) |
| `password` | No | COM password — empty string or omitted means no password |
| `origen` | Yes | Business identifier (warehouse/location code) — not from SDK, defined per device |
| `nombre` | Yes | Human-readable device name for logs |
| `borrarMarcas` | No | `true` to purge device logs after confirmed successful export |

---

## Prerequisites

- .NET Framework 4.8 SDK
- Visual Studio 2022 (MSBuild + vstest.console.exe)
- ZKTeco Standalone SDK x64 DLLs registered (`Register_SDK x64.bat`)
- x64 platform (COM interop is architecture-specific)

---

## Lessons Learned (from SDD Archive Reports)

1. **Old-style .csproj requires explicit `<Compile Include>` for every new .cs file** — SDK-style projects auto-glob, but .NET Framework projects silently exclude files not listed in the .csproj.

2. **ZKTeco `ClearGLog` requires `RefreshData` immediately after** — skipping it leaves the device in an inconsistent state. Only confirmed through SDK demo code analysis, not documented.

3. **`EnableDevice(false)` before any data operation is critical** — without it, concurrent punch-ins during download can corrupt the data stream.

4. **SDK error code `-2` is network, `-6` is wrong password, `-8` is receive timeout, `-307` is connection timeout** — not documented in the official PDF, discovered through testing.

5. **Default TCP port 4370 actually uses UDP** — VPN configuration must explicitly allow UDP traffic.

6. **COM interop blocks `dotnet build`** — the `ResolveComReference` MSBuild task only exists in the full VS 2022 MSBuild, not in the .NET Core SDK.

7. **FluentAssertions 7.x silently breaks .NET Framework builds** — NuGet resolves 7.x without warnings, but the build fails with type-resolution errors. Always pin to `6.12.*`.
