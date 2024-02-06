using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Notes.Interface
{
    public abstract class Logger
    {
        public abstract void Write(object o, bool toFile = true);

        public virtual void WriteLine(object o, bool toFile = true) => Write(o.ToString() + "\n", toFile);
    }
}
