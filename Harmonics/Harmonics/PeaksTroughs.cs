using System;
using System.Collections.Generic;
using Plankton;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Harmonics
{
    public class PeaksTroughs : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the PeaksTroughs class.
        /// </summary>
        public PeaksTroughs()
          : base("PeaksTroughs", "PeaksTroughs",
              "Calculates the number of global/local peaks and troughs",
              "Harmonics", "4 Utility")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("PMesh", "PMesh", "The PlanktonMesh to use topology from", GH_ParamAccess.item);
            pManager.AddMatrixParameter("EigenvectorMatrix", "v", "The eigenvector matrix", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("PeakCount", "peaks", "Count the number of peaks for each mode", GH_ParamAccess.list);
            pManager.AddTextParameter("TroughCount", "troughs", "Count the number of troughs for each mode", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //Global variables
            PlanktonMesh pMesh = new PlanktonMesh();
            DA.GetData(0, ref pMesh);

            Matrix mV = null;
            DA.GetData(1, ref mV);

            //---------------------------------------------------------------

            //Detect number of peaks/troughs of each mode
            List<String> peaksPerMode = new List<String>();
            List<String> troughsPerMode = new List<String>();
            for (int j = 0; j < mV.ColumnCount; j++)
            {
                peaksPerMode.Add(calcNumberOfPeaks(pMesh, mV, j, true));
                troughsPerMode.Add(calcNumberOfPeaks(pMesh, mV, j, false));
            }

            //---------------------------------------------------------------


            //Output
            DA.SetDataList(0, peaksPerMode);
            DA.SetDataList(1, troughsPerMode);
        }

        //Methods

        //----------------------------------------------------PEAKS / TROUGHS -----------------------------------------------//
        //Detect peaks/troughs for specific mode from topology and eigenvector. Peak: max = true, Trough: max = false.
        /* returns a list of two values: number of global peaks and number of local peaks */
        public String calcNumberOfPeaks(PlanktonMesh pMesh, Matrix mV, int modeNumber, bool max)
        {
            int globalPeakCount = 0;
            int localPeakCount = 0;

            //peak or trough
            int mult = 1;
            if (!max)
            {
                mult *= -1;
            }

            //for every vertex test if value is larger/smaller than all of its neighbours
            for (int i = 0; i < pMesh.Vertices.Count; i++)
            {
                //value in vertex
                double val = Math.Round(mV[i, modeNumber], 3);

                //vertex neighbours to compare eigenvector values with
                int[] vNeighbours1R = pMesh.Vertices.GetVertexNeighbours(i);
                int neighboursCount = 0;

                foreach (int j in vNeighbours1R)
                {
                    double valN = Math.Round(mV[j, modeNumber], 3);
                    if (val * mult > valN * mult)
                    {
                        neighboursCount++;
                    }
                }

                //if peak in 1-ring then check for 2-ring as well
                if (neighboursCount == vNeighbours1R.Length)
                {
                    //increase local peak/trough count
                    localPeakCount++;

                    //find vertices in 2-ring to make sure it's a global peak/trough
                    List<int> vNeighbours2R = new List<int>();

                    foreach (int k in vNeighbours1R)
                    {
                        int[] vNeighbours2RTemp = pMesh.Vertices.GetVertexNeighbours(k);
                        foreach (int m in vNeighbours2RTemp)
                        {
                            if (Array.IndexOf(vNeighbours1R, m) == -1 && m != i)
                            {
                                vNeighbours2R.Add(m);
                            }
                        }
                    }

                    //test against values in 2-ring
                    int neighbours2Count = 0;

                    foreach (int n in vNeighbours2R)
                    {
                        double valN2 = Math.Round(mV[n, modeNumber], 3);
                        if (val * mult > valN2 * mult)
                        {
                            neighbours2Count++;
                        }
                    }

                    if (neighbours2Count == vNeighbours2R.Count)
                    {
                        globalPeakCount++;
                    }

                }
            }

            String globalLocalInfo = String.Format("Global: {0} , Local: {1}", globalPeakCount, localPeakCount);

            return globalLocalInfo;
        }


        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                return Properties.Resources.PeaksAndTroughsIcon;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("d51c6446-258c-470a-927c-f1b27689c050"); }
        }
    }
}