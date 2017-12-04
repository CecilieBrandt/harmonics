using System;
using System.Collections.Generic;
using Plankton;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Harmonics
{
    public class Boundary : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the Boundary class.
        /// </summary>
        public Boundary()
          : base("Boundary", "BC",
              "Fix the specified vertices",
              "Harmonics", "1 Matrix")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddMatrixParameter("LaplacianMatrix", "L", "The cotangent or graph Laplacian matrix", GH_ParamAccess.item);
            pManager.AddGenericParameter("PlanktonMesh", "PMesh", "The PlanktonMesh to use topology from", GH_ParamAccess.item);
            pManager.AddPointParameter("FixedPts", "pts", "A list of points to be fixed", GH_ParamAccess.list);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddMatrixParameter("ModifiedLaplacianMatrix", "LBC", "The modified Laplacian matrix with imposed boundary conditions", GH_ParamAccess.item);
            pManager.AddIntegerParameter("k", "k", "The maximum number of eigenvalues/vectors to calculate", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //Global variables
            Matrix mL = null;
            DA.GetData(0, ref mL);

            PlanktonMesh pMesh = null;
            DA.GetData<PlanktonMesh>(1, ref pMesh);

            List<Point3d> fixedPts = new List<Point3d>();
            DA.GetDataList(2, fixedPts);


            //--------------------------------------------------------
            bool mm = isUnitMillimeter(pMesh);
            List<int> fixedPtsIndices = findPtIndices(pMesh, fixedPts, mm);

            int k = pMesh.Vertices.Count - fixedPtsIndices.Count;
            Matrix LBC = null;

            //Runtime error if list is empty
            if (fixedPtsIndices.Count == 0)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No corresponding fixed vertices exist in the PMesh");
            }
            //Warning if not all fixities were found in PMesh
            else if (fixedPtsIndices.Count != fixedPts.Count)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, String.Format("Only {0} number of fixed vertices were found in the Pmesh out of the specified {1}", fixedPtsIndices.Count, fixedPts.Count));
                LBC = updateLBC(mL, fixedPtsIndices, 1000);
            }
            else
            {
                LBC = updateLBC(mL, fixedPtsIndices, 100000);
            }

            //--------------------------------------------------------

            //Output
            DA.SetData(0, LBC);
            DA.SetData(1, k);
        }

        //UNIT EVALUATION
        /* determine whether the model is in mm or m to adjust accuracy */
        public bool isUnitMillimeter(PlanktonMesh pMesh)
        {
            bool mm = false;

            foreach (PlanktonVertex pV in pMesh.Vertices)
            {
                Vector3d pos = new Vector3d(pV.X, pV.Y, pV.Z);
                if (pos.Length > 1000.0)
                {
                    mm = true;
                    continue;
                }
            }
            return mm;
        }


        //FIND INDICES OF FIXED VERTICES
        /*find the corresponding indices for the fixed points in the pMesh */

        public List<int> findPtIndices(PlanktonMesh pMesh, List<Point3d> fixedPts, bool isUnitmm)
        {
            List<int> fixityIndices = new List<int>();
            int numDec = 3;
            if (isUnitmm)
            {
                numDec = 1;
            }

            //Create a list of vertex points with rounded off coordinate values
            List<Point3d> verticesPts = new List<Point3d>();
            for (int j = 0; j < pMesh.Vertices.Count; j++)
            {
                verticesPts.Add(new Point3d(Math.Round(pMesh.Vertices[j].X, numDec), Math.Round(pMesh.Vertices[j].Y, numDec), Math.Round(pMesh.Vertices[j].Z, numDec)));
            }

            //Check against fixed points
            foreach (Point3d pt in fixedPts)
            {
                Point3d ptDec = new Point3d(Math.Round(pt.X, numDec), Math.Round(pt.Y, numDec), Math.Round(pt.Z, numDec));
                for (int i = 0; i < pMesh.Vertices.Count; i++)
                {
                    //compareTo returns 0 if identical
                    if (ptDec.CompareTo(verticesPts[i]) == 0)
                    {
                        fixityIndices.Add(i);
                        continue;
                    }
                }
            }

            return fixityIndices;
        }


        //MODIFY LAPLACIAN MATRIX ACCORDING TO FIXITY
        /*correct matrix acccording to fixity with high value in diagonal and zero in corresponding row/column */

        public Matrix updateLBC(Matrix mL, List<int> fixityIndices, double stiffness)
        {

            foreach (int i in fixityIndices)
            {
                mL[i, i] = stiffness;
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
                return Properties.Resources.BoundaryConditionsIcon;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("23d41f24-2223-4396-8326-beb9661e8601"); }
        }
    }
}