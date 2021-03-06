﻿
namespace Display
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Data;
    using System.Drawing;
    using System.Linq;
    using System.Text;
    using System.Windows.Forms;

    using System.IO;
    using System.Diagnostics;
    using OpenTK;
    using OpenTK.Graphics;
    using OpenTK.Graphics.OpenGL;
    using SickLidar;
    using FieldMap;
    using ZedGraph;
    using CombineBody;

    public partial class LidarOpenGlForm : Form
    {
        #region fields

        private CombineBody.Body3DMap _map;
        
        /// <summary>
        /// graph
        /// </summary>
        private FieldMap.Graph _graph;

        /// <summary>
        /// is loaded?
        /// </summary>
        private bool loaded { get; set; }

        /// <summary>
        /// translate value
        /// </summary>
        private int transX { get; set; }
        private int transY { get; set; }
        private int transZ { get; set; }

        /// <summary>
        /// roatate value
        /// </summary>
        private double angle { get; set; }

        /// <summary>
        /// Crop vertex points
        /// </summary>
        //private List<SickLidar.CartesianPoint> crop;
        private Vector3[] cropPoints;
        private Vector3[] groundPoints;
        private int cropCnt { get; set; }
        private int cropOffset { get; set; }
        private int groundOffset { get; set; }

        /// <summary>
        /// transverse mecartor
        /// </summary>
        private float _tmX { get; set; }
        private float _tmY { get; set; }
        private float _tmZ { get; set; }

        /// <summary>
        /// body information
        /// </summary>
        private double _heading_angle { get; set; }
        private double _body_speed { get; set; }

        /// <summary>
        /// manual key control
        /// </summary>
        private bool isManualControl { get; set; }

        /// <summary>
        /// edge points
        /// </summary>
        private Vector3[] edgePoints;
        //private int edgeOffset { get; set; }

        /// <summary>
        /// ideal path points
        /// </summary>
        private Vector3[] idealPathPoints;

        /// <summary>
        /// gets or sets divider of header position
        /// </summary>
        private double headerX { get; set; }
        private double headerY { get; set; }

        /// <summary>
        /// gets or sets combine model index
        /// </summary>
        private int bodyModelIndex { get; set; }

        #endregion

        #region Constructor

        /// <summary>
        /// basic constructor
        /// </summary>
        public LidarOpenGlForm(int _bodyModelIndex)
        {
            InitializeComponent();

            // initialization graph
            this._graph = new FieldMap.Graph();
            this._graph.CreateGraph(zgc1);

            // initialization OpenGl variable
            this.bodyModelIndex = _bodyModelIndex;
            this._map = new Body3DMap(_bodyModelIndex, false);

            this.loaded = false;
            this.transX = 0;
            this.transY = 0;
            this.transZ = 0;
            this.angle = 0.0;

            //this.crop = new List<SickLidar.CartesianPoint>();
            cropPoints = new Vector3[361 * 50000];
            groundPoints = new Vector3[361 * 50000];
            edgePoints = new Vector3[2 * 50000];
            idealPathPoints = new Vector3[2 * 50000];

            this.cropCnt = 0;
            this.cropOffset = 0;
            this.groundOffset = 0;
            //this.edgeOffset = 0;

            this.isManualControl = false;
        }

        #endregion

        #region Graph

        /// <summary>
        /// Draw traceability map
        /// </summary>
        private void DrawTraceabilityMap()
        {
            PointPairList pplist = new PointPairList();

            // add arrow
            pplist.Add(this._tmX, this._tmY);
            List<double> arrow_end = this._map.ConvertPoint(0.0, 0.1, this._heading_angle);
            pplist.Add(this._tmX + arrow_end[0], this._tmY + arrow_end[1]);

            // add ideal path
            pplist.Add(this.idealPathPoints[0].X, this.idealPathPoints[0].Y);
            pplist.Add(this.idealPathPoints[1].X, this.idealPathPoints[1].Y);

            // add extracted ransac line
            if (this.ran_running == true)
            {
                pplist.Add(this.edgePoints[0].X, this.edgePoints[0].Y);
                pplist.Add(this.edgePoints[1].X, this.edgePoints[1].Y);
            }

            // add body points
            //List<double> body_a = this.ConvertPoint(-0.5, 1.0, this._heading_angle);
            //pplist.Add(this._tmX + body_a[0], this._tmY + body_a[1]);
            //List<double> body_b = this.ConvertPoint(0.5, 1.0, this._heading_angle);
            //pplist.Add(this._tmX + body_b[0], this._tmY + body_b[1]);
            //List<double> body_d = this.ConvertPoint(0.5, -1.0, this._heading_angle);
            //pplist.Add(this._tmX + body_d[0], this._tmY + body_d[1]);
            //List<double> body_c = this.ConvertPoint(-0.5, -1.0, this._heading_angle);
            //pplist.Add(this._tmX + body_c[0], this._tmY + body_c[1]);

            // draw traceability on the graph
            this._graph.TraceabilityGraph(zgc1, pplist, this.ran_running);
        }

        #endregion

        #region OpenGL Methods

        /// <summary>
        /// Draw Body
        /// if _bodyModel is 0 then body model is vy50.
        /// else if _bodyModel is 1 then body model is vy446.
        /// </summary>
        private void DrawBody()
        {
            this._map._body_pose.X = this._tmX;
            this._map._body_pose.Y = this._tmY;
            this._map._body_pose.Z = this._tmZ;
            this._map._body_pose.Angle = this._heading_angle;
            //this._map._auger_received.DT_AUG_MTR = this.DT_AUG_MTR;
            //this._map._auger_received.DT_AUG_CLD = this.DT_AUG_CLD;

            this._map.DrawBody();
            this.GlBodyHeaderPotentiometerTxtBox.Text = this._map.header_meter.ToString("N3");
        }


        /// <summary>
        /// add edge point to vertex array
        /// </summary>
        /// <param name="_list"></param>
        /// <param name="_isRan"></param>
        public void AddEdge(List<SickLidar.CartesianPoint> _list, bool _isRan)
        {
            if (_isRan == true)
            {
                for (int i = 0; i < _list.Count; i++)
                {
                    //edgePoints[i + this.edgeOffset] = new Vector3((float)_list[i].x, (float)_list[i].y, (float)_list[i].z);
                    edgePoints[i] = new Vector3((float)_list[i].x, (float)_list[i].y, (float)_list[i].z);
                }

                //this.edgeOffset += _list.Count;
            }
        }

        /// <summary>
        /// add ideal path point to vertex array
        /// </summary>
        /// <param name="_list"></param>
        public void AddIdealPath(List<SickLidar.CartesianPoint> _list)
        {
            for (int i = 0; i < _list.Count; i++)
            {
                idealPathPoints[i] = new Vector3((float)_list[i].x, (float)_list[i].y, (float)_list[i].z);
            }
        }

        /// <summary>
        /// discriminate point X between crop and ground
        /// </summary>
        private double discriminate_point_x { get; set; }

        /// <summary>
        /// discriminate point Y between crop and ground
        /// </summary>
        private double discriminate_point_y { get; set; }

        /// <summary>
        /// add crop points to list
        /// </summary>
        /// <param name="_list"></param>
        /// <param name="_glIndex"></param>
        public void AddCrop(List<SickLidar.CartesianPoint> _list, int _glIndex)
        {
            this.discriminate_point_x = _list[_glIndex].x;
            this.discriminate_point_y = _list[_glIndex].y;

            for (int i = 0; i < _glIndex; i++)
            {
                //this.crop.Add(new SickLidar.CartesianPoint(_list[i].x, _list[i].y, _list[i].z));
                this.cropPoints[i + this.cropOffset] = new Vector3((float)_list[i].x, (float)_list[i].y, (float)_list[i].z);
            }
            this.cropOffset += _glIndex;

            int gCnt = 0;
            for (int i = _glIndex; i < _list.Count; i++)
            {
                this.groundPoints[gCnt + this.groundOffset] = new Vector3((float)_list[i].x, (float)_list[i].y, (float)_list[i].z);
                gCnt++;
            }
            this.groundOffset += _list.Count - _glIndex;

            this.cropCnt++;            
        }

        /// <summary>
        /// Draw crop stand on the openGL form
        /// </summary>
        private void DrawCrop()
        {
            //// Immediate mode
            //GL.Begin(BeginMode.Points);
            //GL.PointSize(2);
            //GL.Color3(Color.Violet);
            //for (int i = 0; i < this.crop.Count; i++)
            //{
            //    GL.Vertex3(this.crop[i].x, this.crop[i].y, this.crop[i].z);
            //}
            //GL.End();

            // Vertex array mode
            GL.EnableClientState(ArrayCap.VertexArray);
            GL.VertexPointer(3, VertexPointerType.Float, 0, cropPoints);
            GL.Color3(Color.LawnGreen);
            GL.DrawArrays(BeginMode.Points, 0, this.cropOffset - 1);
            GL.DisableClientState(ArrayCap.VertexArray);

            GL.EnableClientState(ArrayCap.VertexArray);
            GL.VertexPointer(3, VertexPointerType.Float, 0, groundPoints);
            GL.Color3(Color.SaddleBrown);
            GL.DrawArrays(BeginMode.Points, 0, this.groundOffset - 1);
            GL.DisableClientState(ArrayCap.VertexArray);

        }

        /// <summary>
        /// draw edge
        /// </summary>
        private void DrawEdge()
        {
            // Vertex array mode
            GL.EnableClientState(ArrayCap.VertexArray);
            GL.VertexPointer(3, VertexPointerType.Float, 0, edgePoints);
            GL.Color3(Color.Red);
            //GL.DrawArrays(BeginMode.Lines, 0, this.edgeOffset - 1);
            GL.DrawArrays(BeginMode.Lines, 0, 3);
            GL.DisableClientState(ArrayCap.VertexArray);

            // Vertex array mode
            GL.EnableClientState(ArrayCap.VertexArray);
            GL.VertexPointer(3, VertexPointerType.Float, 0, idealPathPoints);
            GL.Color3(Color.Navy);
            //GL.DrawArrays(BeginMode.Lines, 0, this.edgeOffset - 1);
            GL.DrawArrays(BeginMode.Lines, 0, 3);
            GL.DisableClientState(ArrayCap.VertexArray);
        }

        /// <summary>
        /// Form Update
        /// </summary>
        public void GlUpdate()
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();

            // glControl invalidate
            glControl1.Invalidate();

            // draw traceability map
            this.DrawTraceabilityMap();

            // save data
            if (this.GlSaveDataCheckBox.Checked == true)
            {
                this.SaveData();
            }

            watch.Stop();
            this.GlElapsedTxtBox.Text =
                watch.Elapsed.TotalMilliseconds.ToString("N3") + " milliseconds";
        }

        #endregion

        #region Debug methods

        private StreamWriter save_to_txt;
        private bool save_ready = false;
        private int save_index = 0;

        /// <summary>
        /// save data to txt file
        /// </summary>
        private void SaveData()
        {
            // open and add data
            if ((this.ran_start == true) && (this.ran_end == false))
            {
                // initialization
                if (this.save_ready == false)
                {
                    this.save_index++;
                    string save_file_name = "result" + Convert.ToString(this.save_index) + ".txt";
                    this.save_to_txt = new StreamWriter(save_file_name);
                    string title =
                        "Read_count" + " " +
                        "Tm_X" + " " +
                        "Tm_Y" + " " +
                        "Body_angle" + " " +
                        "Ideal_path_angle" + " " +
                        "Current_position_to_ideal_path_angle" + " " +
                        "Perpendicular_distance_Ideal" + " " +
                        "Ransac_angle" + " " +
                        "Header_to_ransac_angle" + " " +
                        "Perpencicular_distance_ransac" + " " +
                        "Steer_command" + " " +
                        "Header_command" + " " +
                        "Discriminate_ptX" + " " +
                        "Discriminate_ptY" + " " +
                        "Right_header_ptX" + " " +
                        "Right_header_ptY" + " " +
                        "Avg_Gnd_Hgt";
                    this.save_to_txt.WriteLine(title);

                    this.save_ready = true;
                }

                string read_cnt = Convert.ToString(this.read_count);
                string tm_x = Convert.ToString(this._tmX);
                string tm_y = Convert.ToString(this._tmY);
                string body_angle = Convert.ToString(this._heading_angle);
                string ideal_path_angle = Convert.ToString(this.ideal_angle);
                string gps_angle = Convert.ToString(this.gps_angle);
                string gps_distance = Convert.ToString(this.gps_distance);
                string ran_angle = "0";
                string header_to_ran_angle = "0";
                string ran_distance = "0";
                string steer_cmd = "0";
                string header_cmd = "0";
                string discriminate_ptX = "0";
                string discriminate_ptY = "0";
                string right_header_ptX = Convert.ToString(this.headerX);
                string right_header_ptY = Convert.ToString(this.headerY);
                string avg_gnd_hgt = "0";

                if (this.ran_start == true)
                {
                    // add discriminate point between ground and crop
                    discriminate_ptX = Convert.ToString(this.discriminate_point_x);
                    discriminate_ptY = Convert.ToString(this.discriminate_point_y);

                    // add ransac information to txt file
                    if (this.ran_running == true)
                    {
                        ran_angle = Convert.ToString(this.ran_heading);
                        header_to_ran_angle = Convert.ToString(this.ran_to_header_angle);
                        ran_distance = Convert.ToString(this.ran_current_dist);

                        if (this.is_autonomous_mode == true)
                        {
                            avg_gnd_hgt = Convert.ToString(this.avgGndHgt);
                        }
                    }
                }

                // add body control information to txt file
                if (this.is_autonomous_mode == true)
                {
                    steer_cmd = this.forward_steer_debug_msg;
                    header_cmd = Convert.ToString(this.cmd_header_potentiometer);
                }

                string data = read_cnt + " " + tm_x + " " + tm_y + " " + body_angle + " " + ideal_path_angle + " " +
                    gps_angle + " " + gps_distance + " " + ran_angle + " " + header_to_ran_angle + " " +
                    ran_distance + " " + steer_cmd + " " + header_cmd + " " + discriminate_point_x + " " +
                    discriminate_point_y + " " + right_header_ptX + " " + right_header_ptY + " " + avg_gnd_hgt;

                this.save_to_txt.WriteLine(data);
            }

            // close
            if ((this.ran_running == false) && (this.ran_end == true) && (this.save_ready == true))
            {
                this.save_to_txt.Close();
                this.save_ready = false;
            }

            // debug
            this.GlSaveStateTxtBox.Text = Convert.ToString(this.save_ready);
        }

        /// <summary>
        /// gets or sets current processing count number
        /// </summary>
        private int read_count { get; set; }

        /// <summary>
        /// Body information debug method
        /// </summary>
        /// <param name="_readCnt"></param>
        /// <param name="_tmX"></param>
        /// <param name="_tmY"></param>
        /// <param name="_tmZ"></param>
        /// <param name="_heading_angle"></param>
        /// <param name="_body_speed"></param>
        /// <param name="_header_potentiometer"></param>
        public void BodyInformation(int _readCnt, double _tmX, double _tmY, double _tmZ, double _heading_angle, double _body_speed, int _header_potentiometer)
        {
            this.GlReadCntTxtBox.Text = Convert.ToString(_readCnt);
            this.read_count = _readCnt;
            
            this.GlCurCntTxtBox.Text = Convert.ToString(this.cropCnt);
            
            this.GlTmXTxtBox.Text = _tmX.ToString("N3");
            this._tmX = (float)_tmX;
            
            this.GlTmYTxtBox.Text = _tmY.ToString("N3");
            this._tmY = (float)_tmY;
            
            this.GlTmZTxtBox.Text = _tmZ.ToString("N3");
            this._tmZ = (float)_tmZ;
            
            this.GlBodyHeadingTxtBox.Text = _heading_angle.ToString("N3");
            this._heading_angle = _heading_angle;
            
            this.GlBodySpeedTxtBox.Text = _body_speed.ToString("N3");
            this._body_speed = _body_speed;

            //this.GlBodyHeaderPotentiometerTxtBox.Text = _header_potentiometer.ToString();
            this._map.header_potentiometer = _header_potentiometer;

            this.GlHarvestTimesTxtBox.Text = Convert.ToString(this.harvest_times_count);
        }

        /// <summary>
        /// gets or sets is autonomous mode state
        /// </summary>
        private bool is_autonomous_mode = false;

        /// <summary>
        /// vy446 autonomous check debug method
        /// </summary>
        /// <param name="_is_autonomous_mode"></param>
        public void Vy446AutonomousModeCheckDebug(bool _is_autonomous_mode)
        {
            this.GlAutoModeTxtBox.Text = Convert.ToString(_is_autonomous_mode);
            this.is_autonomous_mode = _is_autonomous_mode;
        }

        /// <summary>
        /// gets or sets ransac start state
        /// </summary>
        private bool ran_start { get; set; }
        
        /// <summary>
        /// gets or sets ransac running state
        /// </summary>
        private bool ran_running { get; set; }
        
        /// <summary>
        /// gets or sets ransac end state
        /// </summary>
        private bool ran_end { get; set; }

        /// <summary>
        /// for harvest count
        /// </summary>
        private bool is_harvest_start = false;

        /// <summary>
        /// gets or sets number of times of harvest
        /// </summary>
        public int harvest_times_count { get; set; }

        /// <summary>
        /// gets or sets distance between ransac points
        /// </summary>
        private double ran_distance_between_points { get; set; }

        /// <summary>
        /// gets or sets harvested area
        /// </summary>
        private double harvested_area { get; set; }

        /// <summary>
        /// ransac state debug method
        /// </summary>
        /// <param name="_ran_start"></param>
        /// <param name="_ran_running"></param>
        /// <param name="_ran_end"></param>
        /// <param name="_ran_distance_between_points"></param>
        public void RansacStateDebug(bool _ran_start, bool _ran_running, bool _ran_end, double _ran_distance_between_points)
        {
            this.GlRanStartTxtBox.Text = Convert.ToString(_ran_start);
            this.ran_start = _ran_start;

            this.GlIsRanTxtBox.Text = Convert.ToString(_ran_running);
            this.ran_running = _ran_running;

            this.GlRanEndTxtBox.Text = Convert.ToString(_ran_end);
            this.ran_end = _ran_end;

            this.GlHarvestDistanceTxtBox.Text = _ran_distance_between_points.ToString("N3");
            this.ran_distance_between_points = _ran_distance_between_points;

            this.GlHarvestedAreaTxtBox.Text = this._map.harvested_quad_area.ToString("N3");
            this.harvested_area = this._map.harvested_quad_area;

            if (this.ran_start == true)
            {
                this.is_harvest_start = true;
            }

            if ((this.ran_end == true) && (this.is_harvest_start == true))
            {
                this.harvest_times_count++;
                this.is_harvest_start = false;
            }
        }

        /// <summary>
        /// gets or sets ransac angle
        /// </summary>
        private double ran_heading { get; set; }

        /// <summary>
        /// gets or sets perpendicular distance between TM point and ransac line
        /// </summary>
        private double ran_current_dist { get; set; }

        /// <summary>
        /// gets or sets average perpendicular distance between TM point and ransac line
        /// </summary>
        private double ran_average_dist { get; set; }

        /// <summary>
        /// gets or sets angle between header and extracted ransac line
        /// </summary>
        private double ran_to_header_angle { get; set; }

        /// <summary>
        /// Ransac result debug
        /// </summary>
        /// <param name="_ran_heading"></param>
        /// <param name="_ran_current_dist"></param>
        /// <param name="_ran_average_dist"></param>
        /// <param name="_ran_to_header_angle"></param>
        public void RansacResultDebug(double _ran_heading, double _ran_current_dist, double _ran_average_dist, double _ran_to_header_angle)
        {
            this.GlRanHeadingTxtBox.Text = _ran_heading.ToString("N3");
            this.ran_heading = _ran_heading;

            this.GlRanDistanceTxtBox.Text = _ran_current_dist.ToString("N3");
            this.ran_current_dist = _ran_current_dist;

            this.GlRanStandDistanceTxtBox.Text = _ran_average_dist.ToString("N3");
            this.ran_average_dist = _ran_average_dist;

            this.GlHeaderRanHeadingTxtBox.Text = _ran_to_header_angle.ToString("N3");
            this.ran_to_header_angle = _ran_to_header_angle;
        }

        /// <summary>
        /// gets or sets forward steering debug message
        /// </summary>
        private string forward_steer_debug_msg { get; set; }

        /// <summary>
        /// Forward Steer Debug
        /// </summary>
        /// <param name="_vy50"></param>
        /// <param name="_vy446"></param>
        /// <param name="_cmd_steer"></param>
        /// <param name="_cmd_hst"></param>
        public void ForwardSteerDebug(bool _vy50, bool _vy446, ushort _cmd_steer, ushort _cmd_hst, string _forward_steer_debug_msg)
        {
            // vy446
            if ((_vy50 == false) && (_vy446 == true))
            {
                this.GlSteerCmdTxtBox.Text = Convert.ToString(_cmd_steer);
                this.GlHstCmdTxtBox.Text = Convert.ToString(_cmd_hst);
                this.GlSteerOperationTxtBox.Text = _forward_steer_debug_msg;
                this.forward_steer_debug_msg = _forward_steer_debug_msg;
            }
        }

        /// <summary>
        /// gets or sets header potentiometer
        /// </summary>
        private ushort cmd_header_potentiometer { get; set; }

        /// <summary>
        /// gets or sets average ground height
        /// </summary>
        private double avgGndHgt { get; set; }

        /// <summary>
        /// header control debug
        /// </summary>
        /// <param name="_cmd_header_potentiometer"></param>
        /// <param name="_karitaka_start_distance"></param>
        /// <param name="_karitaka_end_distance"></param>
        /// <param name="_avgGndHgt"></param>
        public void HeaderControlDebug(ushort _cmd_header_potentiometer, double _karitaka_start_distance, double _karitaka_end_distance, double _avgGndHgt)
        {
            this.GlHeaderPoteniometerTxtBox.Text = Convert.ToString(_cmd_header_potentiometer);
            this.cmd_header_potentiometer = _cmd_header_potentiometer;

            this.GlHeaderStartDistanceTxtBox.Text = _karitaka_start_distance.ToString("N3");
            this.GlHeaderEndDistanceTxtBox.Text = _karitaka_end_distance.ToString("N3");

            this.GlHeaderAvgGndHgtTxtBox.Text = Convert.ToString(_avgGndHgt);
            this.avgGndHgt = _avgGndHgt;
        }

        /// <summary>
        /// gets or sets perpendicular distance between ideal path to gps position
        /// </summary>
        private double gps_distance { get; set; }

        /// <summary>
        /// gets or sets gps angle
        /// </summary>
        private double gps_angle { get; set; }

        /// <summary>
        /// ideal path angle
        /// </summary>
        private double ideal_angle { get; set; }

        /// <summary>
        /// Ideal path to GPS debug
        /// </summary>
        /// <param name="_gps_distance"></param>
        /// <param name="_gps_angle"></param>
        /// <param name="_ideal_angle"></param>
        public void IdealToGpsResultDebug(double _gps_distance, double _gps_angle, double _ideal_angle)
        {
            this.GlGpsDistanceTxtBox.Text = _gps_distance.ToString("N3");
            this.gps_distance = _gps_distance;

            this.GlGpsHeadingTxtBox.Text = _gps_angle.ToString("N3");
            this.gps_angle = _gps_angle;
            
            this.GlIdealHeadingTxtBox.Text = _ideal_angle.ToString("N3");
            this.ideal_angle = _ideal_angle;
        }

        #endregion

        #region Event

        /// <summary>
        /// load
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void glControl1_Load(object sender, EventArgs e)
        {
            this.loaded = true;

            // Yey! .NET Colors can be used directly!
            GL.ClearColor(Color.White);
            GL.Enable(EnableCap.DepthTest);

            this._map.SetupViewport(glControl1);
        }

        /// <summary>
        /// resize event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void glControl1_Resize(object sender, EventArgs e)
        {
            if (!this.loaded)
            {
                return;
            }

            this._map.SetupViewport(glControl1);
            glControl1.Invalidate();
        }

        /// <summary>
        /// GLcontrol paint event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void glControl1_Paint(object sender, PaintEventArgs e)
        {
            // play nice
            if (!this.loaded)
            {
                return;
            }

            this._map.DrawInitialize((float)this.transX, (float)this.transY, (float)this.transZ, this.angle);
            this._map.DrawCoordinates();
            this._map.DrawGround();
            this.DrawBody();
            this.DrawCrop();
            this._map.DrawHarvestedArea(this.ran_start, this.ran_end);
            this.DrawEdge();

            glControl1.SwapBuffers();
            //GL.Flush();
        }

        /// <summary>
        /// keyboard event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void glControl1_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.L:
                    this.transX--;
                    break;

                case Keys.J:
                    this.transX++;
                    break;

                case Keys.I:
                    this.transZ--;
                    break;

                case Keys.K:
                    this.transZ++;
                    break;

                case Keys.U:
                    this.transY--;
                    break;

                case Keys.O:
                    this.transY++;
                    break;

                case Keys.A:
                    this.angle--;
                    break;

                case Keys.S:
                    this.angle++;
                    break;

                case Keys.M:
                    this.isManualControl = true;
                    break;
            }

            if (this.isManualControl == true)
            {
                glControl1.Invalidate();
            }
        }

        /// <summary>
        /// exit event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ExitButton_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        #endregion
    }
}
