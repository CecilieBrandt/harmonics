using System;
using System.Collections.Generic;
using Plankton;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Harmonics
{
    public class GraphLaplacian : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the GraphLaplacian class.
        /// </summary>
        public GraphLaplacian()
          : base("GraphLaplacian", "GraphLaplacian",
              "Calculate the Laplacian matrix with uniform weightings of an arbitrary mesh",
              "Harmonics", "1 Matrix")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("PlanktonMesh", "PMesh", "PlanktonMesh to use topology from", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddMatrixParameter("GraphLaplacian", "L", "The Laplacian matrix with uniform weightings", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //Global variables
            PlanktonMesh pMesh = new PlanktonMesh();
            DA.GetData<PlanktonMesh>(0, ref pMesh);

            //------------------------------------------------------

            //Create LB matrix
            Matrix mL = calcGraphLaplacian(pMesh);

            //------------------------------------------------------

            //Output
            DA.SetData(0, mL);
        }

        // GRAPH LAPLACIAN
        /* valence of vertices in diagonal, -1 if connected, 0 otherwise */

        public Matrix calcGraphLaplacian(PlanktonMesh pMesh)
        {
            Matrix mL = new Matrix(pMesh.Vertices.Count, pMesh.Vertices.Count);

            for (int i = 0; i < pMesh.Vertices.Count; i++)
            {
                for (int j = 0; j < pMesh.Vertices.Count; j++)
                {
                    //diagonal entrities
                    if (i == j)
                    {
                        mL[i, j] = pMesh.Vertices.GetValence(i);
                    }
                    //other entrities
                    else
                    {
                        int halfedge_ij = pMesh.Halfedges.FindHalfedge(i, j);
                        if (halfedge_ij != -1)
                        {
                            mL[i, j] = -1;      //if connected
                        }
                        else
                        {
                            mL[i, j] = 0;
                        }
                    }
                }
            }
            return mL;
        }


        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                return Properties.Resources.GraphLaplacianIcon;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("883e55a9-fafc-4622-8945-dfe0807e9a25"); }
        }
    }
}