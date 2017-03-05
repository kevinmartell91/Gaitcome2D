using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Axes;


namespace Gaitcome2D
{
    class PlottingResults
    {

        #region Declarations
        public PlotModel pmKneeGraphic { get; private set; }
        public PlotModel pmHipGraphic { get; private set; }
        public PlotModel pmAnkleGraphic { get; private set; }
        public PlotModel pmPelvisGraphic { get; private set; }

        LineSeries lsLstKnee;
        LineSeries lsLstHip;
        LineSeries lsLstAnkle;
        LineSeries lsLstPelvis;

        LineSeries lsLstNormalKneeAngles;
        LineSeries lsLstNormalHipAngles;
        LineSeries lsLstNormalPelvisAngles;
        LineSeries lsLstNormalAnkleAngles;

        LineSeries lsLstKneeAxisXY;
        LineSeries lsLstAnkleAxisXY;
        LineSeries lsLstPelvisAxisXY;
        LineSeries lsLstHipAxisXY;
        #endregion


        public PlottingResults(List<double> lstAnkleAngles, List<double> lstKneeAngles, List<double> lstHipAngles, List<double> lstPelvisAngles)
        //public PlottingResults()
        {
            SetLineSeriesAttributes();
            SetLineSeriesAngles(lstAnkleAngles,lstKneeAngles,lstHipAngles,lstPelvisAngles);
            //SetLineSeriesAngles();
            ShowPlotModels();
        }

        private void ShowPlotModels()
        {

            pmKneeGraphic = new PlotModel { Title = "Rodilla" };
            pmKneeGraphic.Series.Add(lsLstKneeAxisXY);
            pmKneeGraphic.Series.Add(lsLstNormalKneeAngles);
            pmKneeGraphic.Series.Add(lsLstKnee);//

            pmAnkleGraphic = new PlotModel { Title = "Tobillo" };
            pmAnkleGraphic.Series.Add(lsLstAnkleAxisXY);
            pmAnkleGraphic.Series.Add(lsLstNormalAnkleAngles);
            pmAnkleGraphic.Series.Add(lsLstAnkle);

            pmPelvisGraphic = new PlotModel { Title = "Pelvis" };
            pmPelvisGraphic.Series.Add(lsLstPelvisAxisXY);
            pmPelvisGraphic.Series.Add(lsLstNormalPelvisAngles);
            pmPelvisGraphic.Series.Add(lsLstPelvis);

            pmHipGraphic = new PlotModel { Title = "Cadera" };
            pmHipGraphic.Series.Add(lsLstHipAxisXY);
            pmHipGraphic.Series.Add(lsLstNormalHipAngles);
            pmHipGraphic.Series.Add(lsLstHip);

            exportToSVG(pmAnkleGraphic);
        }

        private void SetLineSeriesAngles(List<double> lstAnkleAngles, List<double> lstKneeAngles, List<double> lstHipAngles, List<double> lstPelvisAngles)
        {

            int j = 0;
            int lengthInt = 3;

            int lengthAngles = lstAnkleAngles.Count();
            double dx = 101 * 1.0 / lengthAngles;
            double dxng = 101 * 1.0 / (lengthAngles * 1.0 / lengthInt);


            //Setting MIN & MAX Y_axis
            lsLstKneeAxisXY.Points.Add(new DataPoint(0,90 ));
            lsLstKneeAxisXY.Points.Add(new DataPoint(1,-20 ));
            lsLstAnkleAxisXY.Points.Add(new DataPoint(0,30 ));
            lsLstAnkleAxisXY.Points.Add(new DataPoint(1,-30 ));
            lsLstPelvisAxisXY.Points.Add(new DataPoint(0,30 ));
            lsLstPelvisAxisXY.Points.Add(new DataPoint(1,-5 ));
            lsLstHipAxisXY.Points.Add(new DataPoint(0,60 ));
            lsLstHipAxisXY.Points.Add(new DataPoint(1,-20 ));

            for (int i = 0; i < lengthAngles; i++)
            {
                if (i % lengthInt == 0)
                {
                    lsLstNormalKneeAngles.Points.Add(new DataPoint(j * dxng, lstKneeAngles[i]));
                    lsLstNormalAnkleAngles.Points.Add(new DataPoint(j * dxng, lstAnkleAngles[i]));
                    lsLstNormalPelvisAngles.Points.Add(new DataPoint(j * dxng, lstPelvisAngles[i]));
                    lsLstNormalHipAngles.Points.Add(new DataPoint(j * dxng, lstHipAngles[i]));

                    j++;
                }

                lsLstKnee.Points.Add(new DataPoint(i * dx, lstKneeAngles[i]));
                lsLstAnkle.Points.Add(new DataPoint(i * dx, lstAnkleAngles[i]));
                lsLstPelvis.Points.Add(new DataPoint(i * dx, lstPelvisAngles[i]));
                lsLstHip.Points.Add(new DataPoint(i * dx, lstHipAngles[i]));
            }

            j++;
        }

        private void SetLineSeriesAttributes()
        {
            double[] lstDbl = { 34, 45, 56, 67, 89, 10 };

            //Setting graph atttibutes to patient angles
           lsLstKnee = new LineSeries
           {
               Title = "Rodilla derecha",
               Smooth = true,
               Color = OxyColors.SkyBlue,
               StrokeThickness = 2,
           };
            lsLstHip = new LineSeries
           {
               Title = "Cadera derecha",
               Smooth = true,
               Color = OxyColors.SkyBlue,
               StrokeThickness = 2
           };
            lsLstPelvis = new LineSeries
           {
               Title = "Pelvis derecha",
               Smooth = true,
               Color = OxyColors.SkyBlue,
               StrokeThickness = 2
           };
            lsLstAnkle = new LineSeries
            {
                Title = "Tobillo derecha",
                Smooth = true,
                Color = OxyColors.SkyBlue,
                StrokeThickness = 2
            };

            //Setting Atributes to NORMAL GAIT ANGLES
            #region Sample of setting attributes
            /*
            lsLstNormalKneeAngles = new LineSeries
            {
                Title = "Tobillo derecha Normal",
                Background = OxyColors.WhiteSmoke,
                BrokenLineColor = OxyColors.SkyBlue,
                BrokenLineStyle = LineStyle.LongDash,
                BrokenLineThickness = 999,
                CanTrackerInterpolatePoints = true,
                Dashes = lstDbl,
                Color = OxyColors.LightGray,
                DataFieldX = "DataFieldX",
                DataFieldY = "DataFieldY",
                Font = "3",
                FontSize = 3,
                FontWeight = 2,
                LabelFormatString = "LabelFormatString",
                LabelMargin = 6, // dafault 6
                LineJoin = OxyPlot.LineJoin.Round,
                LineLegendPosition = OxyPlot.Series.LineLegendPosition.Start,
                LineStyle = OxyPlot.LineStyle.DashDotDot,
                MarkerFill = OxyColors.Red,
                MarkerSize = 6,
                MarkerStroke = OxyColors.Yellow,
                MarkerStrokeThickness = 2,
                MarkerType = OxyPlot.MarkerType.Circle,
                MinimumSegmentLength = 5,
                RenderInLegend = true,
                Selectable = true,
                SelectionMode = OxyPlot.SelectionMode.All,//CIO 
                Smooth = true,
                StrokeThickness = 54,
                Tag = new Obj(2, 2),
                TextColor = OxyColors.Violet,
                ToolTip = "ToolTip",
                TrackerFormatString = "TrackerFormatString",
                TrackerKey = "TrackerKey"
                //XAxisKey = "XAxisKey",
                //YAxisKey = "YAxisKey"

            };
            */
            #endregion
            
            lsLstNormalKneeAngles = new LineSeries
            {
                Title = "Rangos normales",
                Color = OxyColors.LightGray,
                Smooth = true,
                StrokeThickness = 20
            };
            lsLstNormalAnkleAngles = new LineSeries
            {
                Title = "Rangos normales",
                BrokenLineThickness = 20,
                Color = OxyColors.LightGray,
                Smooth = true,
                StrokeThickness = 20
            };
            lsLstNormalHipAngles = new LineSeries
            {
                Title = "Rangos normales",
                Color = OxyColors.LightGray,
                Smooth = true,
                StrokeThickness = 20
            };
            lsLstNormalPelvisAngles = new LineSeries
            {
                Title = "Rangos normales",
                Color = OxyColors.LightGray,
                Smooth = true,
                StrokeThickness = 20
            };

            //setting Axis MIN & MAX of ht Grapich
            lsLstKneeAxisXY = new LineSeries
            {
                Title = "Min and max of Y Axis",
                BrokenLineThickness = 0,
                Color = OxyColors.White,
                RenderInLegend = false,
                Selectable = false,
                Smooth = false
            };
            lsLstAnkleAxisXY = new LineSeries
            {
                Title = "Min and max of Y Axis",
                BrokenLineThickness = 0,
                Color = OxyColors.White,
                RenderInLegend = false,
                Selectable = false,
                Smooth = false
            };
            lsLstPelvisAxisXY = new LineSeries
            {
                Title = "Min and max of Y Axis",
                BrokenLineThickness = 0,
                Color = OxyColors.White,
                RenderInLegend = false,
                Selectable = false,
                Smooth = false
            };
            lsLstHipAxisXY = new LineSeries
            {
                Title = "Min and max of Y Axis",
                BrokenLineThickness = 0,
                Color = OxyColors.White,
                RenderInLegend = false,
                Selectable = false,
                Smooth = false
            };
        }

        private void exportToSVG(PlotModel  plotModel)
        {
            using (var stream = System.IO.File.Create(@"D:\Projects\Gaitcom\Gaitcome2D\img.svg"))
            {
                var exporter = new SvgExporter { Width = 600, Height = 400 };
                exporter.Export(plotModel, stream);
            }
        }
        
    }
    public class Obj
    {
        double x { get; set; }
        double y { get; set; }

        public Obj(double x, double y)
        {
            this.x = x;
            this.y = y;
        }
    }
}
