using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Plankton;
using System.Drawing;
using Grasshopper;
using Grasshopper.Kernel.Data;

namespace Harmonics
{
    public class ModeDisplay : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ModeDisplay class.
        /// </summary>
        public ModeDisplay()
          : base("ModeDisplay", "ModeDisplay",
              "Creates the necessary data to visualise each mode on the Grasshopper canvas using Squid",
              "Harmonics", "3 Display")
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
            pManager.AddRectangleParameter("Frame", "rec", "The frame", GH_ParamAccess.item);
            pManager.AddCurveParameter("FacesAsCurves", "crv", "The outline of the faces as curves", GH_ParamAccess.list);
            pManager.AddColourParameter("FillColours", "fill", "The face fill colours", GH_ParamAccess.tree);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Global variables
            PlanktonMesh pMesh = new PlanktonMesh();
            DA.GetData(0, ref pMesh);

            Matrix mV = null;
            DA.GetData(1, ref mV);


            //Calculate
            Rectangle frame = createFrame(pMesh);
            List<Polyline> facePolygons = extractFacePolygons(pMesh, frame);
            DataTree<Color> modeColourTree = modeColouringTree(pMesh, mV);


            //Output
            DA.SetData(0, frame);
            DA.SetDataList(1, facePolygons);
            DA.SetDataTree(2, modeColourTree);
        }

        //Methods

        //Create bitmap frame as bounding box of vertices in mesh
        public Rectangle createFrame(PlanktonMesh pMesh)
        {
            //desired max frame dimension
            int dim = 300;

            List<double> xCoord = new List<double>();
            List<double> yCoord = new List<double>();

            foreach (PlanktonVertex pV in pMesh.Vertices)
            {
                xCoord.Add(pV.X);
                yCoord.Add(pV.Y);
            }

            double xRange = xCoord.Max() - xCoord.Min();
            double yRange = yCoord.Max() - yCoord.Min();


            //Compare and scale according to desired dimension
            double ratio = dim / xRange;
            if (yRange > xRange)
            {
                ratio = dim / yRange;
            }

            int w = (int)Math.Ceiling(xRange * ratio);
            int h = (int)Math.Ceiling(yRange * ratio);

            Rectangle rec = new Rectangle(0, 0, w, h);

            return rec;
        }


        //Create curves from mesh faces within frame
        public List<Polyline> extractFacePolygons(PlanktonMesh pMesh, Rectangle frame)
        {
            List<Polyline> facePolygons = new List<Polyline>();

            //Map to frame domain
            List<double> xCoord = new List<double>();
            List<double> yCoord = new List<double>();

            foreach (PlanktonVertex pV in pMesh.Vertices)
            {
                xCoord.Add(pV.X);
                yCoord.Add(pV.Y);
            }

            double xMin = xCoord.Min();
            double xMax = xCoord.Max();
            double yMin = yCoord.Min();
            double yMax = yCoord.Max();

            double xRange = xMax - xMin;
            double yRange = yMax - yMin;

            //Map all vertices into this domain
            List<Point3d> verticesMapped = new List<Point3d>();
            foreach (PlanktonVertex pV in pMesh.Vertices)
            {
                double x_normal = (pV.X - xMin) / (xRange);
                double x_map = 0 + x_normal * (frame.Width);

                double y_normal = (pV.Y - yMin) / (yRange);
                double y_map = 0 + y_normal * (frame.Height);

                verticesMapped.Add(new Point3d(x_map, y_map, 0));
            }


            //Run through faces
            for (int i = 0; i < pMesh.Faces.Count; i++)
            {
                int[] faceVertexIndices = pMesh.Faces.GetFaceVertices(i);

                List<Point3d> faceVertices = new List<Point3d>();
                foreach (int j in faceVertexIndices)
                {
                    faceVertices.Add(verticesMapped[j]);
                }
                //Closed polyline
                faceVertices.Add(faceVertices[0]);

                facePolygons.Add(new Polyline(faceVertices));
            }

            return facePolygons;
        }


        //Colour faces as average of nodal values of a specific mode
        public List<Color> modeColouring(PlanktonMesh pMesh, Matrix mV, int modeNumber)
        {
            List<Color> faceColours = new List<Color>();

            List<double> faceAverageValue = new List<double>();

            //run through faces
            for (int i = 0; i < pMesh.Faces.Count; i++)
            {
                double valAverage = 0.0;

                int[] faceVertices = pMesh.Faces.GetFaceVertices(i);
                foreach (int j in faceVertices)
                {
                    valAverage += mV[j, modeNumber];
                }

                valAverage /= faceVertices.Length;
                valAverage *= 1000;
                faceAverageValue.Add(valAverage);
            }

            //Map to colour domain
            double valMin = faceAverageValue.Min();
            double valMax = faceAverageValue.Max();
            double valRange = valMax - valMin;

            if (Convert.ToInt32(valRange) != 0)
            {
                for (int k = 0; k < faceAverageValue.Count; k++)
                {
                    double t_normal = (faceAverageValue[k] - valMin) / (valRange);
                    double t_map = 0 + t_normal * (255 - 0);
                    int t_color = Convert.ToInt32(t_map);
                    faceColours.Add(Color.FromArgb(t_color, t_color, t_color));
                }
            }
            //pure translation i.e. constant eigenvector
            else
            {
                //default colour black
                int t_color = 0;

                for (int m = 0; m < pMesh.Faces.Count; m++)
                {
                    faceColours.Add(Color.FromArgb(t_color, t_color, t_color));
                }
            }

            return faceColours;
        }


        //Create colour tree for all modes in input matrix
        public DataTree<Color> modeColouringTree(PlanktonMesh pMesh, Matrix mV)
        {
            DataTree<Color> colourTree = new DataTree<Color>();

            for (int i = 0; i < mV.ColumnCount; i++)
            {
                List<Color> modeColours = modeColouring(pMesh, mV, i);
                GH_Path path = new GH_Path(i);

                foreach (Color c in modeColours)
                {
                    colourTree.Add(c, path);
                }
            }

            return colourTree;
        }


        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                return Properties.Resources.modeVisualiserIcon;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("3ce222f5-dcee-4ff5-9a98-f1655b90ca51"); }
        }
    }
}