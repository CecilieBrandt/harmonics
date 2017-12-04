using System;
using System.Collections.Generic;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System.Drawing;

namespace Harmonics
{
    public class AutoSlider : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the AutoSlider class.
        /// </summary>
        public AutoSlider()
          : base("AutoSlider", "AutoSlider",
              "Automatically generate weight sliders by pressing a button",
              "Harmonics", "4 Utility")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("AddSliderButton", "button", "A new slider is created when the button is pressed", GH_ParamAccess.item);
            pManager.AddNumberParameter("SliderValues", "val", "The slider values", GH_ParamAccess.list);
            pManager[1].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Weights", "w", "The list of weights specified by the added sliders", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //Global variables
            bool addSlider = false;
            DA.GetData(0, ref addSlider);

            List<double> sliderValues = new List<double>();
            DA.GetDataList(1, sliderValues);


            //Add slider method
            addWeightSlider(addSlider);


            //Output
            DA.SetDataList(0, sliderValues);
        }

        //Methods

        //Automatically add a slider to the canvas
        void addWeightSlider(bool addSlider)
        {
            if (addSlider)
            {
                //Instantiate new slider
                GH_NumberSlider slider = new GH_NumberSlider();
                slider.CreateAttributes();                              //set to default values

                //Customisation of values
                int inputCount = this.Params.Input[1].SourceCount;      //count the number of connected inputs
                slider.Attributes.Pivot = new PointF((float)this.Attributes.DocObject.Attributes.Bounds.Left - slider.Attributes.Bounds.Width - 30, (float)this.Params.Input[1].Attributes.Bounds.Y + inputCount * 30);
                slider.Slider.Minimum = -1;
                slider.Slider.Maximum = 1;
                slider.Slider.DecimalPlaces = 2;
                slider.SetSliderValue((decimal)0.00);
                slider.NickName = String.Format("w{0}", inputCount);

                //Add the slider to the canvas
                OnPingDocument().AddObject(slider, false);
                this.Params.Input[1].AddSource(slider);
            }
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                return Properties.Resources.AutoSliderIcon;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("072751d8-4f3a-45a6-9dd9-5e176a6f05c4"); }
        }
    }
}