﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace Walt.Framework.Core
{
    public static class AssemblyHelp
    {
 
        public static Type GetTypeByAssemblyNameAndClassName(Assembly assemebly,string clssName)
        {
            return assemebly.GetType(clssName);
        }

        public static Type GetTypeByCurrentAssemblyNameAndClassName(string clssName,Assembly assembly)
        {
            return assembly.GetType(clssName,true);
        }

        public static Assembly GetAssemblyByteByAssemblyName(string path,string assemblyName)
        {
            if(!Directory.Exists(path))
            {
                throw new DirectoryNotFoundException(string.Format("路径不存在，路径：{0}",path));
            }
            string fullPath=Path.Combine(path,assemblyName);
            if(System.IO.File.Exists(fullPath))
            {
                return Assembly.LoadFrom(fullPath);
            }
            return null;
        }
    }
}
