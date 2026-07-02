using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;

namespace T4Studio.Vsix.Debug
{
    [ComVisible(true)]
    [Guid("8b4f3c2a-1d5e-4f6a-9b8c-7a3d2e1f5c6d")]
    public class T4DebugEngineLauncher : IDebugEngineLaunch2
    {
        private static readonly Guid engineGuid = new Guid("8b4f3c2a-1d5e-4f6a-9b8c-7a3d2e1f5c6d");

        public int LaunchSuspended(string pszServer, IDebugPort2 pPort, string pszExe, string pszArgs, string pszDir, string bstrEnv, string pszOptions, enum_LAUNCH_FLAGS dwLaunchFlags, uint hStdInput, uint hStdOutput, uint hStdError, IDebugEventCallback2 pCallback, out IDebugProcess2 ppProcess)
        {
            ppProcess = null;
            return VSConstants.E_NOTIMPL;
        }

        public int ResumeProcess(IDebugProcess2 process)
        {
            return VSConstants.S_OK;
        }

        public int CanTerminateProcess(IDebugProcess2 pProcess)
        {
            return VSConstants.S_OK;
        }

        public int TerminateProcess(IDebugProcess2 pProcess)
        {
            return VSConstants.S_OK;
        }

        public int GetEngineID(out Guid pguidEngine)
        {
            pguidEngine = engineGuid;
            return VSConstants.S_OK;
        }

        public int EnumPrograms(out IEnumDebugPrograms2 ppEnum)
        {
            ppEnum = null;
            return VSConstants.E_NOTIMPL;
        }

        public int Attach(IDebugProgram2[] rgpPrograms, IDebugProgramNode2[] rgpProgramNodes, uint celtPrograms, IDebugEventCallback2 pCallback, enum_ATTACH_REASON dwReason)
        {
            return VSConstants.E_NOTIMPL;
        }

        public int CreatePendingBreakpoint(IDebugBreakpointRequest2 pBPRequest, out IDebugPendingBreakpoint2 ppPendingBP)
        {
            ppPendingBP = null;
            return VSConstants.E_NOTIMPL;
        }

        public int SetException(EXCEPTION_INFO[] pException)
        {
            return VSConstants.E_NOTIMPL;
        }

        public int RemoveSetException(EXCEPTION_INFO[] pException)
        {
            return VSConstants.E_NOTIMPL;
        }

        public int RemoveAllSetExceptions(ref Guid guidType)
        {
            return VSConstants.E_NOTIMPL;
        }

        public int DestroyProgram(IDebugProgram2 pProgram)
        {
            return VSConstants.S_OK;
        }

        public int ContinueFromSynchronousEvent(IDebugEvent2 pEvent)
        {
            return VSConstants.S_OK;
        }

        public int SetLocale(ushort wLangID)
        {
            return VSConstants.S_OK;
        }

        public int SetRegistryRoot(string pszRegistryRoot)
        {
            return VSConstants.S_OK;
        }

        public int SetMetric(string pszMetric, object varValue)
        {
            return VSConstants.S_OK;
        }

        public int CauseBreak()
        {
            return VSConstants.S_OK;
        }
    }
}

