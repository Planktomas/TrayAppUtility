using System;

namespace TrayAppUtility
{
    [AttributeUsage(AttributeTargets.Method)]
    public class TrayActionAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class TrayDefaultAttribute : Attribute { }
}
