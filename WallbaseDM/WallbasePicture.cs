namespace WallbaseDM
{
    class WallbasePicture
    {
        private string name;
        private string referer;
        private Purity purity;
	    private string url;
	    private string localPath;
		private bool downloaded;

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

	    public string Url
	    {
		    get { return url; }
			set { url = value; }
	    }

	    public string LocalPath
	    {
			get { return localPath; }
			set { localPath = value; }
	    }

	    public bool Downloaded
	    {
		    get { return downloaded; }
		    set { downloaded = value; }
	    }

	    public WallbasePicture(string name, string referer, Purity purity)
        {
            this.name = name;
            this.referer = referer;
            this.purity = purity;
	        url = string.Empty;
	        localPath = string.Empty;
		    downloaded = false;
        }
    }
}
