using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BillionsSaveManager
{
    // Keep templates and values until display time, including nested recovery errors.
    internal sealed class LocalizedText
    {
        private readonly string template;
        private readonly object[] arguments;
        public LocalizedText(string template, params object[] arguments) { this.template = template; this.arguments = arguments; }
        public override string ToString() { return L.T(template, arguments.Select(Render).ToArray()); }
        private static object Render(object value)
        {
            var error = value as Exception;
            if (error != null) return error.Message;
            var lines = value as IEnumerable<object>;
            if (lines != null) return string.Join("\n",lines.Select(item => Convert.ToString(Render(item))).ToArray());
            return value;
        }
    }
    internal sealed class LocalizedIOException : IOException
    {
        private readonly LocalizedText text;
        public LocalizedIOException(string template, params object[] args) : this(null,template,args) { }
        public LocalizedIOException(Exception inner, string template, params object[] args) : base("",inner) { text = new LocalizedText(template,args); }
        public override string Message { get { return text.ToString(); } }
    }
    internal sealed class LocalizedInvalidOperationException : InvalidOperationException
    {
        private readonly LocalizedText text;
        public LocalizedInvalidOperationException(string template, params object[] args) { text = new LocalizedText(template,args); }
        public override string Message { get { return text.ToString(); } }
    }
    internal sealed class LocalizedDirectoryNotFoundException : DirectoryNotFoundException
    {
        private readonly LocalizedText text;
        public LocalizedDirectoryNotFoundException(string template, params object[] args) { text = new LocalizedText(template,args); }
        public override string Message { get { return text.ToString(); } }
    }
    internal sealed class LocalizedFileNotFoundException : FileNotFoundException
    {
        private readonly LocalizedText text;
        public LocalizedFileNotFoundException(string template, string filename) : base("",filename) { text = new LocalizedText(template); }
        public override string Message { get { return text.ToString(); } }
    }
}
