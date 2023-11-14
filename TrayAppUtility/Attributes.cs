using System;

namespace TrayAppUtility
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class TrayActionAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class TrayDefaultAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class NoLogAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class AutorunAttribute : Attribute 
    {
        public string? TimeSpanString { get; }

        public AutorunAttribute(string? timeSpanString = null)
        {
            TimeSpanString = timeSpanString;
        }
    }
}
