// ****************************************************************************
// 
// Copyright (C) 2005-2022 Doom9 & al
// 
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
// 
// ****************************************************************************

using System;
using System.Collections.Generic;
using System.Text;

namespace MeGUI
{
    public class DeinterlaceFilter
    {
        private string script;
        private string title;
        public DeinterlaceFilter(string title, string script)
        {
            this.title = title;
            this.script = script;
        }
        public override string ToString()
        {
            return this.title;
        }

        public string Script
        {
            get { return script; }
            set { script = value; }
        }

        public string Title
        {
            get { return title; }
            set { title = value; }
        }
    }
}
