using System;
using System.Drawing;
using Grasshopper.Kernel;

namespace Harmonics
{
    public class HarmonicsInfo : GH_AssemblyInfo
    {
        public override string Name
        {
            get
            {
                return "Harmonics";
            }
        }
        public override Bitmap Icon
        {
            get
            {
                //Return a 24x24 pixel bitmap to represent this GHA library.
                return null;
            }
        }
        public override string Description
        {
            get
            {
                //Return a short string describing the purpose of this GHA library.
                return "";
            }
        }
        public override Guid Id
        {
            get
            {
                return new Guid("089f8f08-e914-45c5-94a7-e5add4ca32fe");
            }
        }

        public override string AuthorName
        {
            get
            {
                //Return a string identifying you or your company.
                return "Cecilie Brandt-Olsen";
            }
        }
        public override string AuthorContact
        {
            get
            {
                //Return a string representing your preferred contact details.
                return "";
            }
        }
    }
}
