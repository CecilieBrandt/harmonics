using System;
using System.Collections.Generic;
using Plankton;
using PlanktonGh;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System.Drawing;
using System.Linq;

namespace Harmonics
{
    public class ModeDisplayRhino : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ModeDisplayRhino class.
        /// </summary>
        public ModeDisplayRhino()
          : base("ModeDisplayRhino", "RhinoDisplay",
              "Display the mode shape catalogue in the Rhino viewport",
              "Harmonics", "3 Display")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("PMesh", "PMesh", "The PlanktonMesh to use topology from", GH_ParamAccess.item);
            pManager.AddMatrixParameter("Eigenvectors", "v", "The eigenvectors to display", GH_ParamAccess.item);
            pManager.AddIntegerParameter("RowCount", "rowCount", "The number of modes to be displayed in a row", GH_ParamAccess.item, 10);
            pManager.AddNumberParameter("Spacing", "spacing", "The spacing between meshes as a multiplier of the width/height", GH_ParamAccess.item, 0.1);
            pManager.AddIntegerParameter("ColourOption", "cOption", "0: grey scale, 1: absolute grey scale", GH_ParamAccess.item, 0);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "Mesh", "Mesh catalogue", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Global variables
            PlanktonMesh pMesh = null;
            DA.GetData(0, ref pMesh);

            Matrix mV = null;
            DA.GetData(1, ref mV);

            int rowCount = 5;
            DA.GetData(2, ref rowCount);
            if (rowCount < 1)
            {
                rowCount = 1;
            }
            else if (rowCount > mV.ColumnCount)
            {
                rowCount = mV.ColumnCount;
            }

            double spacingMultiplier = 0.1;
            DA.GetData(3, ref spacingMultiplier);
            if(spacingMultiplier < 0.0)
            {
                spacingMultiplier = 0.0;
            }


            int cOption = 0;
            DA.GetData(4, ref cOption);
            if (cOption < 0)
            {
                cOption = 0;
            }
            else if (cOption > 1)
            {
                cOption = 1;
            }


            //Calculate
            double w, h;
            Point3d ptCurrent = getFrameProperties(pMesh, out w, out h);

            List<Point3d> gridPts = createGrid(w, h, spacingMultiplier, mV.ColumnCount, rowCount);

            List<PlanktonMesh> modePMeshes = new List<PlanktonMesh>();
            foreach(Point3d ptNew in gridPts)
            {
                modePMeshes.Add(moveMesh(pMesh, ptCurrent, ptNew));
            }

            //Convert PMeshes to normal meshes and spary with colour
            List<Mesh> colourMeshes = new List<Mesh>();

            for (int i = 0; i < mV.ColumnCount; i++)
            {
                Mesh rhinoMesh = modePMeshes[i].ToRhinoMesh();

                List<Color> vertexColours = mapToColour(mV, i, cOption);

                //test that the same number of colours exist as there are vertices in the converted Rhino mesh
                if (vertexColours.Count == rhinoMesh.Vertices.Count)
                {
                    rhinoMesh.VertexColors.CreateMonotoneMesh(Color.White);

                    for (int j = 0; j < rhinoMesh.VertexColors.Count; j++)
                    {
                        rhinoMesh.VertexColors[j] = vertexColours[j];
                    }
                }
                else
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, String.Format("The colouring of the Rhino Mesh failed due to inconsistency between the number of vertex colours ({0}) and the number of vertices in the Rhino mesh ({1}). Error due to a pMesh of n-gons with n > 4", vertexColours.Count, rhinoMesh.Vertices.Count));
                }

                colourMeshes.Add(rhinoMesh);
            }


            //Output
            DA.SetDataList(0, colourMeshes);
        }

        //Methods

        //Get mesh centre and XY bbox dimension
        public Point3d getFrameProperties(PlanktonMesh pMesh, out double w, out double h)
        {
            List<double> xCoord = new List<double>();
            List<double> yCoord = new List<double>();
            Point3d cPt = new Point3d(0, 0, 0);

            w = 0.0;
            h = 0.0;

            foreach (PlanktonVertex pV in pMesh.Vertices)
            {
                xCoord.Add(pV.X);
                yCoord.Add(pV.Y);
            }

            double xRange = xCoord.Max() - xCoord.Min();
            double yRange = yCoord.Max() - yCoord.Min();

            cPt.X = xCoord.Min() + xRange * 0.5;
            cPt.Y = yCoord.Min() + yRange * 0.5;

            w = xRange;
            h = yRange;

            return cPt;
        }


        //Create grid of points
        public List<Point3d> createGrid(double w, double h, double spacingMultiplier, int numberOfModes, int rowCount)
        {
            List<Point3d> gridPts = new List<Point3d>();

            double spacing = Math.Max(w, h) * spacingMultiplier;

            int countX = 0;
            int countY = 0;

            for (int i = 0; i < numberOfModes; i++)
            {
                double x = (w + spacing) * countX;
                double y = (h + spacing) * countY;

                gridPts.Add(new Point3d(x, y, 0));

                countX++;

                if ((i+1) % rowCount == 0)
                {
                    countX = 0;
                    countY++;
                }
            }

            return gridPts;
        }


        //Create mesh at new location
        public PlanktonMesh moveMesh(PlanktonMesh pMesh, Point3d ptCurrent, Point3d ptNew)
        {
            PlanktonMesh pMeshNew = new PlanktonMesh(pMesh);
            Vector3d move = new Vector3d(ptNew - ptCurrent);

            for(int i=0; i<pMeshNew.Vertices.Count; i++)
            {
                pMeshNew.Vertices[i].X += Convert.ToSingle(move.X);
                pMeshNew.Vertices[i].Y += Convert.ToSingle(move.Y);
                pMeshNew.Vertices[i].Z += Convert.ToSingle(move.Z);
            }

            return pMeshNew;
        }


        //Map nodal values into vertex colours for a specific mode
        public List<Color> mapToColour(Matrix mV, int modeNumber, int option)
        {
            //list to contain vertex colours
            List<Color> vertexColours = new List<Color>();

            //scale nodal values to make sure that rounding to integers doesn't result in zero values
            List<double> vertexValuesScale = new List<double>();
            for (int k = 0; k < mV.RowCount; k++)
            {
                double val = mV[k, modeNumber] * 1000;
                //Colour as absolute values
                if (option == 1)
                {
                    val = Math.Abs(val);
                }
                vertexValuesScale.Add(val);
            }

            //check domain range of nodal values
            double domainRange = vertexValuesScale.Max() - vertexValuesScale.Min();
            int t_color;

            //map value from nodalvalue domain to 0 - 255 domain if not constant values
            if (Convert.ToInt32(domainRange) != 0)
            {
                for (int i = 0; i < mV.RowCount; i++)
                {
                    double t_normal = (vertexValuesScale[i] - vertexValuesScale.Min()) / (domainRange);
                    double t_map = 0 + t_normal * (255 - 0);
                    t_color = Convert.ToInt32(t_map);

                    vertexColours.Add(Color.FromArgb(t_color, t_color, t_color));
                }
            }
            else
            {
                for (int j = 0; j < mV.RowCount; j++)
                {
                    t_color = 0;
                    vertexColours.Add(Color.FromArgb(t_color, t_color, t_color));
                }
            }
            return vertexColours;
        }


        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                return Properties.Resources.ModeVisualiserRhinoIcon;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("73af9869-e036-47a7-a863-5a7de86cce8f"); }
        }
    }
}