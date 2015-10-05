using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TextConverter
{
    class Page
    {
        private List<StringBuilder> lines { get; set; }
        private string size { get; set; }

        public List<StringBuilder> Lines
        {
            get { return lines; }
            set { lines = value; }
        }

        public string Size
        {
            get { return size; }
            set { size = value; }
        }

        public Page()
        { }

        public Page(List<StringBuilder> Lines, string Size = "LETTER")
        {
            this.lines = Lines;
            this.size = Size;
        }
    }
}
