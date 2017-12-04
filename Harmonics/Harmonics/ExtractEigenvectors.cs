using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Harmonics
{
    public class ExtractEigenvectors : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ExtractEigenvectors class.
        /// </summary>
        public ExtractEigenvectors()
          : base("ExtractEigenvectors", "ExtractEigs",
              "Extract the eigenvectors specified by indices",
              "Harmonics", "1 Matrix")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddMatrixParameter("EigenVectorMatrix", "v", "The matrix containing all the eigenvectors", GH_ParamAccess.item);
            pManager.AddIntegerParameter("EigenvectorIndices", "i", "The indices specifying the eigenvectors to be extracted", GH_ParamAccess.list);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddMatrixParameter("ReducedEigenvectorMatrix", "vReduced", "The reduced eigenvector matrix according to the specified indices", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //Global variables
            Matrix mV = null;
            DA.GetData(0, ref mV);

            List<int> eigsIndices = new List<int>();
            DA.GetDataList(1, eigsIndices);

            //----------------------------------------------------

            Matrix mV_reduced = createReducedEigsM(mV, eigsIndices);

            //----------------------------------------------------


            //Output
            DA.SetData(0, mV_reduced);
        }

        //Methods

        //Create new reduced eigenvector matrix from specified indices
        public Matrix createReducedEigsM(Matrix mV, List<int> eigsIndices)
        {
            Matrix mV_reduced = new Matrix(mV.RowCount, eigsIndices.Count);

            //for each vertex
            for (int i = 0; i < mV.RowCount; i++)
            {
                //for each eigenvector index
                for (int j = 0; j < eigsIndices.Count; j++)
                {
                    mV_reduced[i, j] = mV[i, eigsIndices[j]];
                }
            }
            return mV_reduced;
        }


        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                return Properties.Resources.ExtractEigenvaluesIcon;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("d9868bbe-f571-476b-bacb-3a9fec6971a3"); }
        }
    }
}