using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WallbaseDM
{
    class WallbasePicture
    {
        private string name;
        private string referer;
        private Purity purity;

        public string Referer
        {
            get { return referer; }
        }

        public string Name
        {
            get { return name; }
        }

        public Purity Purity
        {
            get { return purity; }
        }

        public WallbasePicture(string name, string referer, Purity purity)
        {
            this.name = name;
            this.referer = referer;
            this.purity = purity;
        }
    }
}
