﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotesInterface.UiController
{
    public interface IUiLayout
    {
        int IndexOfChild(IUiLayout child);

        void RemoveSelf();
    }
}