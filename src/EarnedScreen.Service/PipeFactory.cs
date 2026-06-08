using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace EarnedScreen.Service;

/// <summary>
/// Creates named-pipe server instances with an ACL that lets the user-session app connect to
/// pipes owned by the SYSTEM service. Authenticated users get read/write; SYSTEM gets full control.
/// </summary>
internal static class PipeFactory
{
    public static NamedPipeServerStream CreateServer(string name)
    {
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
            PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
            AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            name,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            inBufferSize: 0,
            outBufferSize: 0,
            pipeSecurity: security);
    }
}
