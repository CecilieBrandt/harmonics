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
            pManager.AddIntegerParameter("RowCount", "rowCount", "The number of modes to be displayed in a row", GH_ParamAccess.item, 5);
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


            int cOption = 0;
            DA.GetData(3, ref cOption);
            if (cOption < 0)
            {
                cOption = 0;
            }
            else if (cOption > 1)
            {
                cOption = 1;
            }


            //------------------------------------------------------------------------------

            //Create PMesh bounding box
            Rectangle bbox = createBoundingBox(pMesh);

            //Create a gridstructure of rectangles
            List<Rectangle> grid = gridStructure(bbox, mV.ColumnCount, rowCount);

            //Create planktonmesh for each rectangle
            List<PlanktonMesh> modePMeshes = new List<PlanktonMesh>();
            foreach (Rectangle frame in grid)
            {
                modePMeshes.Add(framedPMesh(pMesh, frame));
            }

            //Scaled frames to highligt the specified index
            //List<Polyline> scaledFrames = createdScaledFrames(grid);
            //Polyline highlight = scaledFrames[index];

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

            //------------------------------------------------------------------------

            //Output
            DA.SetDataList(0, colourMeshes);
        }

        //Methods

        //Create a rectangle as the mesh bounding box in XY plane
        public Rectangle createBoundingBox(PlanktonMesh pMesh)
        {
            List<double> xCoord = new List<double>();
            List<double> yCoord = new List<double>();

            foreach (PlanktonVertex pV in pMesh.Vertices)
            {
                xCoord.Add(pV.X);
                yCoord.Add(pV.Y);
            }

            double xRange = xCoord.Max() - xCoord.Min();
            double yRange = yCoord.Max() - yCoord.Min();

            int w = (int)Math.Ceiling(xRange);
            int h = (int)Math.Ceiling(yRange);

            Rectangle rec = new Rectangle(0, 0, w, h);

            return rec;
        }


        //Rectangles arranged in grid structure with n modes in a row
        public List<Rectangle> gridStructure(Rectangle bbox, int numberOfModes, int rowCount)
        {
            List<Rectangle> grid = new List<Rectangle>();

            int w = bbox.Width;
            int h = bbox.Height;

            int spacing = (int)Math.Ceiling((double)w / 20);

            int countX = 0;
            int countY = 0;

            for (int i = 1; i <= numberOfModes; i++)
            {
                int x = (w + spacing) * countX;
                int y = -(h + spacing) * countY;

                grid.Add(new Rectangle(x, y, w, h));

                countX++;

                if (i % rowCount == 0)
                {
                    countX = 0;
                    countY++;
                }
            }
            return grid;
        }


        //Create PMesh within a rectangular frame
        public PlanktonMesh framedPMesh(PlanktonMesh pMesh, Rectangle frame)
        {
            PlanktonMesh pMeshFrame = new PlanktonMesh(pMesh);

            //Map to frame domain
            List<double> xCoord = new List<double>();
            List<double> yCoord = new List<double>();

            foreach (PlanktonVertex pV in pMeshFrame.Vertices)
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
            foreach (PlanktonVertex pV in pMeshFrame.Vertices)
            {
                double x_normal = (pV.X - xMin) / (xRange);
                double x_map = frame.X + x_normal * (frame.Width);

                double y_normal = (pV.Y - yMin) / (yRange);
                double y_map = frame.Y + y_normal * (frame.Height);

                pV.X = (float)x_map;
                pV.Y = (float)y_map;
                pV.Z = (float)0.0;
            }

            return pMeshFrame;
        }



        //Map nodal values into vertex colurs for a specific mode
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


        //Scale highlighted rectangle
        public Polyline scaleRectangle(Rectangle rec)
        {
            System.Drawing.Point pos = rec.Location;
            int w = rec.Width;
            int h = rec.Height;

            int spacing = (int)Math.Ceiling((double)w / 20);
            double offset = spacing / 2.0;

            List<Point3d> scaledCornerPts = new List<Point3d>();
            scaledCornerPts.Add(new Point3d(pos.X - offset, pos.Y - offset, 0));
            scaledCornerPts.Add(new Point3d(pos.X + w + offset, pos.Y - offset, 0));
            scaledCornerPts.Add(new Point3d(pos.X + w + offset, pos.Y + h + offset, 0));
            scaledCornerPts.Add(new Point3d(pos.X - offset, pos.Y + h + offset, 0));
            scaledCornerPts.Add(scaledCornerPts[0]);

            Polyline pl = new Polyline(scaledCornerPts);

            return pl;
        }


        //Create a new list of scaled rectangles as polylines
        public List<Polyline> createdScaledFrames(List<Rectangle> frames)
        {
            List<Polyline> scaledFrames = new List<Polyline>();

            foreach (Rectangle rec in frames)
            {
                scaledFrames.Add(scaleRectangle(rec));
            }

            return scaledFrames;
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