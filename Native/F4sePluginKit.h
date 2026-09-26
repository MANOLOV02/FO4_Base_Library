// F4sePluginKit.h - the ONE seat of what every F4SE plugin of this workspace needs to hook the engine
// without F4SE's headers: the version block, the errors-only log, the PE section lookup, the byte
// signature scanner (EXACTLY one match or nothing), and the redirection of a `call rel32` at its call
// site through a stub reserved near it.
//
// Moved out of NPC_Manager_FO4_LoadBake (SafeScrap phase D, 26-sep, user's decision: one scanner for
// LoadBake and SafeScrap). The code and its log messages are LoadBake's, unchanged: "Not hooking." is the
// positive control the build guard (FO4_Base_Library\EmbeddedNativePlugin.targets) looks for in the .dll.
//
// Every lookup takes the MODULE BASE as a parameter: in the game it is the executable
// (GetModuleHandleW(nullptr)); a probe maps Fallout4.exe from disk as an image and runs the SAME
// resolution code against it, without writing anything.
//
// Header-only (inline), C++17, no dependency beyond <windows.h>.
#pragma once

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shlobj.h>
#include <cstdarg>
#include <cstdio>
#include <cstring>
#include <share.h>   // _SH_DENYWR: we write, but it can still be READ live
#include <string>

#pragma comment(lib, "shell32.lib")

namespace f4kit
{

// ============================================================================================
// F4SE version block
// Copied from f4se-0.7.9/f4se/PluginAPI.h (shipped in 'Fallout 4\src\f4se-0.7.9.tar.gz').
// ============================================================================================
struct F4SEPluginVersionData
{
    enum { kVersion = 1 };
    UINT32 dataVersion;
    UINT32 pluginVersion;
    char   name[256];
    char   author[256];
    UINT32 addressIndependence;
    UINT32 structureIndependence;
    UINT32 compatibleVersions[16];
    UINT32 seVersionRequired;
    UINT32 reservedNonBreaking;
    UINT32 reservedBreaking;
    UINT8  reserved[512];
};

constexpr UINT32 MakeExeVersion(UINT32 major, UINT32 minor, UINT32 build)
{
    return ((major & 0xFF) << 24) | ((minor & 0xFF) << 16) | ((build & 0xFFF) << 4);
}

constexpr UINT32 kRuntime_1_10_163 = MakeExeVersion(1, 10, 163);
constexpr UINT32 kRuntime_1_10_984 = MakeExeVersion(1, 10, 984);
constexpr UINT32 kRuntime_1_11_240 = MakeExeVersion(1, 11, 240);

// ============================================================================================
// Log - ERRORS ONLY. The file is created only when there is something to say.
// ============================================================================================
// TWO THREADS can write the log at once (LoadBake's predicate hangs off queued tasks). Without the lock,
// two simultaneous `fopen_s` calls clobber the handle and the lines come out interleaved exactly when the
// log is the only thing we have to understand what happened.
inline FILE*       g_log = nullptr;
inline std::string g_logPath;
inline SRWLOCK     g_logLock = SRWLOCK_INIT;

// Documents\My Games\Fallout4\F4SE\<fileName>
inline void InitLog(const char* fileName)
{
    PWSTR docs = nullptr;
    if (FAILED(SHGetKnownFolderPath(FOLDERID_Documents, 0, nullptr, &docs))) return;
    char base[MAX_PATH]{};
    WideCharToMultiByte(CP_ACP, 0, docs, -1, base, MAX_PATH, nullptr, nullptr);
    CoTaskMemFree(docs);
    g_logPath = std::string(base) + "\\My Games\\Fallout4\\F4SE\\" + fileName;
}

inline void LogLineV(const char* fmt, va_list a)
{
    AcquireSRWLockExclusive(&g_logLock);
    if (!g_log)
    {
        if (g_logPath.empty() || (g_log = _fsopen(g_logPath.c_str(), "w", _SH_DENYWR)) == nullptr)
        { ReleaseSRWLockExclusive(&g_logLock); return; }
    }
    vfprintf(g_log, fmt, a);
    fputc(0x0A, g_log);
    fflush(g_log);
    ReleaseSRWLockExclusive(&g_logLock);
}

// Optional second destination of the errors: a probe that runs the resolution outside the game sets it to see
// WHY a resolution failed (in the game it stays null and only the log gets them).
inline void (*g_errSink)(const char* line) = nullptr;

// An error: always written (the only thing an errors-only log writes).
inline void Err(const char* fmt, ...)
{
    va_list a;
    va_start(a, fmt);
    if (g_errSink)
    {
        va_list b;
        va_copy(b, a);
        char buf[512];
        vsnprintf(buf, sizeof buf, fmt, b);
        va_end(b);
        g_errSink(buf);
    }
    LogLineV(fmt, a);
    va_end(a);
}

// ============================================================================================
// PE sections and signatures
// ============================================================================================
struct Range { unsigned char* start; size_t len; };

// Signature bytes plus a per-byte "fixed" mask: the wildcards fall on call/jcc/rip displacements, which is
// precisely what changes between builds.
struct Signature { const unsigned char* bytes; const bool* fixed; size_t n; };

// The section `name` (e.g. ".text", ".data", ".rdata") of the image at `base`.
inline bool GetSection(unsigned char* base, const char* name, Range& out)
{
    if (!base) return false;
    auto dos = (IMAGE_DOS_HEADER*)base;
    if (dos->e_magic != IMAGE_DOS_SIGNATURE) return false;
    auto nt = (IMAGE_NT_HEADERS64*)(base + dos->e_lfanew);
    if (nt->Signature != IMAGE_NT_SIGNATURE) return false;
    auto sec = IMAGE_FIRST_SECTION(nt);
    const size_t nameLen = strlen(name);
    for (WORD i = 0; i < nt->FileHeader.NumberOfSections; ++i, ++sec)
        if (memcmp(sec->Name, name, nameLen) == 0 && (nameLen == 8 || sec->Name[nameLen] == 0))
        {
            out.start = base + sec->VirtualAddress;
            out.len   = sec->Misc.VirtualSize;
            return true;
        }
    return false;
}

inline bool GetTextRange(unsigned char* base, Range& out) { return GetSection(base, ".text", out); }

// STOP - IT DEMANDS EXACTLY ONE MATCH. With two we do not know which one it is, and guessing here means
// writing over engine code at random.
inline unsigned char* FindUnique(const Range& r, const Signature& f, const char* who)
{
    unsigned char* found = nullptr;
    int n = 0;
    for (size_t i = 0; i + f.n <= r.len; ++i)
    {
        bool ok = true;
        for (size_t j = 0; j < f.n; ++j)
            if (f.fixed[j] && r.start[i + j] != f.bytes[j]) { ok = false; break; }
        if (!ok) continue;
        if (++n > 1) { Err("[sig:%s] %d matches; expected 1. Not hooking.", who, n); return nullptr; }
        found = r.start + i;
    }
    if (!found) Err("[sig:%s] not present in .text. Unsupported game version. Not hooking.", who);
    return found;
}

// Reads the target of a `call rel32`. nullptr if the byte is not E8.
inline void* CallTarget(unsigned char* site, const char* who)
{
    if (*site != 0xE8) { Err("[call:%s] 0x%p is not E8 (byte %02X). Not hooking.", who, site, *site); return nullptr; }
    INT32 d = 0;
    memcpy(&d, site + 1, 4);
    return site + 5 + d;
}

// The absolute target of a rip-relative operand: `instr` is the instruction, `dispOffset` where its disp32
// sits inside it, `instrLen` its length (the disp is relative to the NEXT instruction).
inline unsigned char* RipTarget(unsigned char* instr, size_t dispOffset, size_t instrLen)
{
    INT32 d = 0;
    memcpy(&d, instr + dispOffset, 4);
    return instr + instrLen + d;
}

// ============================================================================================
// Redirecting a call
// ============================================================================================
// Our DLL lives more than 2 GB away from the exe, so a 5-byte `call rel32` cannot reach it. A page is
// reserved NEAR the target holding 16-byte stubs that jump absolute.
inline unsigned char* AllocNear(unsigned char* nearAddr)
{
    SYSTEM_INFO si{};
    GetSystemInfo(&si);
    const size_t gran = si.dwAllocationGranularity;
    auto base = (uintptr_t)nearAddr & ~(uintptr_t)(gran - 1);
    for (size_t step = gran; step < 0x60000000ull; step += gran)
        for (int dir = 0; dir < 2; ++dir)
        {
            uintptr_t dst = dir ? base + step : base - step;
            if (dst < 0x10000) continue;
            if (auto p = (unsigned char*)VirtualAlloc((void*)dst, gran,
                    MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE))
                return p;
        }
    return nullptr;
}

// Hands out 16-byte stubs from ONE SINGLE page (one allocation granularity, 64 KB, is plenty for the few
// sites a plugin hooks). `RedirectCall` verifies reach anyway before writing, so a site out of range is
// reported and not hooked, instead of jumping somewhere arbitrary.
inline unsigned char* g_page     = nullptr;
inline size_t         g_pageUsed = 0;
inline size_t         g_pageSize = 0;

inline unsigned char* StubNear(unsigned char* nearAddr)
{
    if (!g_page)
    {
        SYSTEM_INFO si{};
        GetSystemInfo(&si);
        g_pageSize = si.dwAllocationGranularity;
        g_page     = AllocNear(nearAddr);
        g_pageUsed = 0;
    }
    if (!g_page) return nullptr;
    if (g_pageUsed + 16 > g_pageSize) return nullptr;
    unsigned char* s = g_page + g_pageUsed;
    g_pageUsed += 16;
    return s;
}

// Redirects ONE `call rel32` to a function of ours and returns its original target.
//
// No function prologue is touched and no trampoline is needed: the disp32 of the `E8` is rewritten at the
// CALL SITE, so the other callers of that function notice nothing. The `E8` is a 32-bit relative and our
// DLL lives more than 2 GB from the exe, so it cannot reach: the call points at a 14-byte stub reserved
// NEARBY, and the stub jumps absolute.
inline void* RedirectCall(unsigned char* site, void* ours, const char* who)
{
    if (*site != 0xE8)
    { Err("[hook:%s] 0x%p is not an E8 call (byte %02X). Not hooking.", who, site, *site); return nullptr; }

    INT32 disp = 0;
    memcpy(&disp, site + 1, 4);
    void* original = site + 5 + disp;

    unsigned char* stub = StubNear(site);
    if (!stub)
    { Err("[hook:%s] could not get memory within +-2GB of 0x%p. Not hooking.", who, site); return nullptr; }

    stub[0] = 0xFF; stub[1] = 0x25;                     // jmp qword ptr [rip+0]
    memset(stub + 2, 0, 4);
    memcpy(stub + 6, &ours, 8);

    INT64 rel = (INT64)stub - (INT64)(site + 5);
    if (rel > INT32_MAX || rel < INT32_MIN)
    { Err("[hook:%s] the stub ended up out of reach of 0x%p. Not hooking.", who, site); return nullptr; }

    DWORD old = 0;
    if (!VirtualProtect(site + 1, 4, PAGE_EXECUTE_READWRITE, &old))
    { Err("[hook:%s] VirtualProtect failed at 0x%p (err %lu). Not hooking.", who, site, GetLastError()); return nullptr; }
    INT32 d32 = (INT32)rel;
    memcpy(site + 1, &d32, 4);
    VirtualProtect(site + 1, 4, old, &old);
    FlushInstructionCache(GetCurrentProcess(), site, 5);
    return original;
}

} // namespace f4kit
