using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TextConverter
{
    class PrintFile
    {
        private List<Page> pages { get; set; }
        private string frontunderlay { get; set; }
        private string backunderlay { get; set; }
        private string plex { get; set; }

        public List<Page> Pages
        {
            get { return pages; }
            set { pages = value; }
        }

        public string FrontUnderlay
        {
            get { return frontunderlay; }
            set { frontunderlay = value; }
        }

        public string BackUnderlay
        {
            get { return backunderlay; }
            set { backunderlay = value; }
        }

        public string Plex
        {
            get { return plex; }
            set { plex = value; }
        }

        public PrintFile()
        { }

        public PrintFile(List<Page> Pages, string Plex = "SIMPLEX", string FrontUnderlay = "", string BackUnderlay = "")
        {
            this.pages = Pages;
            this.plex = Plex;
            this.frontunderlay = FrontUnderlay;
            this.backunderlay = BackUnderlay;
        }
    }
}
