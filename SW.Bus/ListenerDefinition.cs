using System;
using System.Reflection;

namespace SW.Bus;

public class ListenerDefinition
{
    public Type ServiceType { get; set; }
    public Type MessageType { get; set; }
    public string MessageTypeName { get; set; }
    public MethodInfo Method { get; set; }
    public MethodInfo FailMethod { get; set; }
}