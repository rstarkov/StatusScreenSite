using System;
using System.Linq;
using System.Reflection;
using System.Text;
using RT.Util.ExtensionMethods;

namespace StatusScreenSite
{
    interface ITypescriptDto
    {
        DateTime ValidUntilUtc { get; }
    }

    [AttributeUsage(AttributeTargets.Field)]
    class TsNullableAttribute : Attribute
    {
    }

    static class TypescriptDto
    {
        public static void GenerateTypescriptFile()
        {
            var file = new StringBuilder();
            foreach (var type in Assembly.GetEntryAssembly().GetTypes().Where(t => t.GetInterfaces().Contains(typeof(ITypescriptDto))))
            {
                //file.Append
            }
        }
    }
}
