//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------


using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using Microsoft.Kinect;
using System.Globalization;
using System.Windows.Controls;
using System.Data;
using MySql.Data;
using MySql.Data.MySqlClient;
using MySql.Data.Entity;
using System.Net.Sockets;
using System.Text;

using System.Net;

namespace Microsoft.Samples.Kinect.SkeletonBasics
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Width of output drawing
        /// </summary>
        private const float RenderWidth = 640.0f;

        /// <summary>
        /// Height of our output drawing
        /// </summary>
        private const float RenderHeight = 480.0f;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of body center ellipse
        /// </summary>
        private const double BodyCenterThickness = 10;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Brush used to draw skeleton center point
        /// </summary>
        private readonly Brush centerPointBrush = Brushes.Blue;

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently tracked
        /// </summary>
        private readonly Pen trackedBonePen = new Pen(Brushes.Green, 6);

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// Drawing group for skeleton rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;
        
        //Memoria para guardar serie de caracteres/letras
        string cadena, letra, anterior = "NONE";

        //Obtenemos los segundos de la hora local
        DateTime hora_inicio = DateTime.Now;

        //Variables utiles
        string[] pivote = new string[2];

        //Conexión a Base de Datos
         MySqlConnection conexion = new MySqlConnection("SERVER=localhost;" + "DATABASE=charly;" + "UID=root;" + "PASSWORD=;");
        
        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            conexion.Open();
            InitializeComponent();
            conexion.Clone();
        }

        /// <summary>
        /// Draws indicators to show which edges are clipping skeleton data
        /// </summary>
        /// <param name="skeleton">skeleton to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private static void RenderClippedEdges(Skeleton skeleton, DrawingContext drawingContext)
        {
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, RenderHeight - ClipBoundsThickness, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, RenderHeight));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(RenderWidth - ClipBoundsThickness, 0, ClipBoundsThickness, RenderHeight));
            }
        }

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            // Display the drawing using our image control
            Image.Source = this.imageSource;
            



            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            if (null != this.sensor)
            {
                // Turn on the skeleton stream to receive skeleton frames
                this.sensor.SkeletonStream.Enable();
                
                // Add an event handler to be called whenever there is new color frame data
                this.sensor.SkeletonFrameReady += this.SensorSkeletonFrameReady;

                // Start the sensor!
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }

            if (null == this.sensor)
            {
                this.statusBarText.Text = Properties.Resources.NoKinectReady;
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
            }
        }

        /// <summary>
        /// Event handler for Kinect sensor's SkeletonFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons = new Skeleton[0];

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                }
            }

            using (DrawingContext dc = this.drawingGroup.Open())
            {
                // Draw a transparent background to set the render size
                dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

                if (skeletons.Length != 0)
                {
                    foreach (Skeleton skel in skeletons)
                    {
                        RenderClippedEdges(skel, dc);

                        if (skel.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            this.DrawBonesAndJoints(skel, dc);
                        }
                        else if (skel.TrackingState == SkeletonTrackingState.PositionOnly)
                        {
                            dc.DrawEllipse(
                            this.centerPointBrush,
                            null,
                            this.SkeletonPointToScreen(skel.Position),
                            BodyCenterThickness,
                            BodyCenterThickness);
                        }
                    }
                }

                // prevent drawing outside of our render area
                this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            }
        }

        //Función para Encontrar Ángulos Ideales
        public int anguloEsperado(double angulo )
        {
            int angulo_ideal = -1;

            //Ángulos Ideales
            int[] array=  {90,70,45,21,0};
            //Rangos para determinar el ángulo
            int[,] rangos = {{90,81},{80,56},{55,35},{34,11},{10,0}};
            for (int i = 0; i < 5; i++)
            {  
                if(rangos[i,1]<=angulo && angulo<= rangos[i,0])
                {
                    angulo_ideal = array[i];
                }
            }


            return angulo_ideal;
        }

        //Función para determinar el Cuadrante dónde
        //se encuentra la mano
        private int cuadranteMano(double p_x, double p_y, double horizontal, double vertical)
        {
            //Cuadrantes = {2, 1}, {3, 4}
            int cuadrante = 0;

            //Para saber si está a la izquierda de la linea vertical
            if (p_x <= horizontal)
            {
                //para saber si está arriba de la linea horizontal
                if (p_y <= vertical)
                {
                    cuadrante = 2;
                }
                else
                {
                    cuadrante = 3;
                }
            }
            else
            {
                //para saber si está arriba de la linea horizontal
                if (p_y <= horizontal)
                {
                    cuadrante = 1;
                }
                else
                {
                    cuadrante = 4;
                }
            }
            return cuadrante;
        }

        /// <summary>
        /// Draws a skeleton's bones and joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext)
        {
            string mano_1 = "";
            string mano_2 = "";
            //Cuadrantes = {2, 1}, {3, 4}
            int cuadrante = 0, cuadrante2 = 0;
            
            // Render Torso
            this.DrawBone(skeleton, drawingContext, JointType.Head, JointType.ShoulderCenter);
            Joint joint0 = skeleton.Joints[JointType.ShoulderCenter];

            //Linea base Horizontal            
            drawingContext.DrawLine(new Pen(Brushes.Blue, 4), new System.Windows.Point(5, this.SkeletonPointToScreen(joint0.Position).Y+50),
                                    new System.Windows.Point(635,this.SkeletonPointToScreen(joint0.Position).Y+50));
            double base_horizontal = this.SkeletonPointToScreen(joint0.Position).Y + 50;

            //Coordenadas base Horizontal
            drawingContext.DrawText(new FormattedText(5+ "," +(this.SkeletonPointToScreen(joint0.Position).Y + 50),
                                    CultureInfo.GetCultureInfo("en-us"), FlowDirection.LeftToRight,
                                      new Typeface("Verdana"), 36, System.Windows.Media.Brushes.Yellow),
                                   new System.Windows.Point(5, this.SkeletonPointToScreen(joint0.Position).Y + 50));
            drawingContext.DrawText(new FormattedText(635+ "," +(this.SkeletonPointToScreen(joint0.Position).Y + 50),
                                    CultureInfo.GetCultureInfo("en-us"), FlowDirection.LeftToRight,
                                      new Typeface("Verdana"), 36, System.Windows.Media.Brushes.Yellow),
                                   new System.Windows.Point(635-120, this.SkeletonPointToScreen(joint0.Position).Y + 50));


            //linea base Vertical
            drawingContext.DrawLine(new Pen(Brushes.Blue, 4), new System.Windows.Point(this.SkeletonPointToScreen(joint0.Position).X, 5),
                                    new System.Windows.Point(this.SkeletonPointToScreen(joint0.Position).X, 435));
            double base_vertical = this.SkeletonPointToScreen(joint0.Position).X;

            //Coordenadas BaseVertical
            drawingContext.DrawText(new FormattedText(this.SkeletonPointToScreen(joint0.Position).X+ "," +5,
                                    CultureInfo.GetCultureInfo("en-us"), FlowDirection.LeftToRight,
                                      new Typeface("Verdana"), 36, System.Windows.Media.Brushes.Yellow),
                                   new System.Windows.Point(this.SkeletonPointToScreen(joint0.Position).X, 5));
            drawingContext.DrawText(new FormattedText(this.SkeletonPointToScreen(joint0.Position).X + "," + 435,
                                    CultureInfo.GetCultureInfo("en-us"), FlowDirection.LeftToRight,
                                      new Typeface("Verdana"), 36, System.Windows.Media.Brushes.Yellow),
                                   new System.Windows.Point(this.SkeletonPointToScreen(joint0.Position).X, 435));

            // Right Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight);            
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight);
            joint0 = skeleton.Joints[JointType.ElbowRight];
            this.DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight);
            this.DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight);

            Joint joint1 = skeleton.Joints[JointType.HandRight];
            //Obtener el cuadrante para Reconocimiento
            //Para saber si está a la izquierda de la linea vertical
            if (this.SkeletonPointToScreen(joint1.Position).X <= base_vertical)
            {
                //para saber si está arriba de la linea horizontal
                if (this.SkeletonPointToScreen(joint1.Position).Y <= base_horizontal)
                {
                    cuadrante = 2;
                }
                else
                {
                    cuadrante = 3;
                }
            }
            else
            {
                //para saber si está arriba de la linea horizontal
                if (this.SkeletonPointToScreen(joint1.Position).Y <= base_horizontal)
                {
                    cuadrante = 1;
                }
                else
                {
                    cuadrante = 4;
                }
            }

            //Linea HombroDerecho a ManoDerecha - > color Rojo
            drawingContext.DrawLine(new Pen(Brushes.Red, 6), this.SkeletonPointToScreen(joint0.Position),
                                    this.SkeletonPointToScreen(joint1.Position));

            //Coordenadas HombroDerecho
            /*drawingContext.DrawText(new FormattedText(this.SkeletonPointToScreen(joint0.Position).X + "," + this.SkeletonPointToScreen(joint0.Position).Y,
                                    CultureInfo.GetCultureInfo("en-us"), FlowDirection.LeftToRight,
                                      new Typeface("Verdana"), 18, System.Windows.Media.Brushes.Yellow),
                                   this.SkeletonPointToScreen(joint0.Position));*/

            double angulo = Math.Atan2(Math.Abs(this.SkeletonPointToScreen(joint1.Position).Y - this.SkeletonPointToScreen(joint0.Position).Y),
                                        Math.Abs(this.SkeletonPointToScreen(joint0.Position).X - this.SkeletonPointToScreen(joint1.Position).X));
            angulo = angulo * 180 / Math.PI;

            //Guardar datos para Reconocimiento
            mano_2 = "(" + cuadrante + "," + anguloEsperado(angulo) + ")";

            //Mostrar Cuadrante/Angulo ManoDerecha        this.SkeletonPointToScreen(joint1.Position).X + "," + this.SkeletonPointToScreen(joint1.Position).Y
            drawingContext.DrawText(new FormattedText(mano_2,
                                    CultureInfo.GetCultureInfo("en-us"), FlowDirection.LeftToRight,
                                      new Typeface("Verdana"), 18, System.Windows.Media.Brushes.Yellow),
                                   this.SkeletonPointToScreen(joint1.Position));
            

            //Angulo Linea Derecha
            /*drawingContext.DrawText(new FormattedText("Angulo: " + Math.Round(angulo,4),
                                    CultureInfo.GetCultureInfo("en-us"), FlowDirection.LeftToRight,
                                      new Typeface("Verdana"), 18, System.Windows.Media.Brushes.Yellow), new Point(480, 5));*/



            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.Spine);

            // Left Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft);
            joint0 = skeleton.Joints[JointType.ElbowLeft];
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft);
            this.DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft);

            joint1 = skeleton.Joints[JointType.HandLeft];
            //Obtener el cuadrante para Reconocimiento
            //Para saber si está a la izquierda de la linea vertical
            if (this.SkeletonPointToScreen(joint1.Position).X <= base_vertical)
            {
                //para saber si está arriba de la linea horizontal
                if (this.SkeletonPointToScreen(joint1.Position).Y <= base_horizontal)
                {
                    cuadrante = 2;
                }
                else
                {
                    cuadrante = 3;
                }
            }
            else
            {
                //para saber si está arriba de la linea horizontal
                if (this.SkeletonPointToScreen(joint1.Position).Y <= base_horizontal)
                {
                    cuadrante = 1;
                }
                else
                {
                    cuadrante = 4;
                }
            }

            //Linea HombroIzquierdo a ManoIzquierda - > color Rojo
            drawingContext.DrawLine(new Pen(Brushes.Red, 6), this.SkeletonPointToScreen(joint0.Position),
                                    this.SkeletonPointToScreen(joint1.Position));

            //Coordenadas HombroIzquierdo
            /*drawingContext.DrawText(new FormattedText(this.SkeletonPointToScreen(joint0.Position).X + "," + this.SkeletonPointToScreen(joint0.Position).Y,
                                    CultureInfo.GetCultureInfo("en-us"), FlowDirection.LeftToRight,
                                      new Typeface("Verdana"), 18, System.Windows.Media.Brushes.Yellow),
                                   this.SkeletonPointToScreen(joint0.Position));*/
            
            angulo = Math.Atan2(Math.Abs(this.SkeletonPointToScreen(joint1.Position).Y - this.SkeletonPointToScreen(joint0.Position).Y),
                                        Math.Abs(this.SkeletonPointToScreen(joint0.Position).X - this.SkeletonPointToScreen(joint1.Position).X));
            angulo = angulo * 180 / Math.PI;
            //Guardar datos para Reconocimiento
            mano_1 = "(" + cuadrante + "," + anguloEsperado(angulo) + ")";
            //Mostrar Cuadrante/Angulo ManoIzquierda       this.SkeletonPointToScreen(joint1.Position).X + "," + this.SkeletonPointToScreen(joint1.Position).Y+ 
            drawingContext.DrawText(new FormattedText(mano_1,
                                    CultureInfo.GetCultureInfo("en-us"), FlowDirection.LeftToRight,
                                      new Typeface("Verdana"), 18, System.Windows.Media.Brushes.Yellow),
                                   this.SkeletonPointToScreen(joint1.Position));            
            
            //Angulo Linea Izquierda
            /*drawingContext.DrawText(new FormattedText("Angulo: " + Math.Round(angulo,4),
                                    CultureInfo.GetCultureInfo("en-us"), FlowDirection.LeftToRight,
                                      new Typeface("Verdana"), 18, System.Windows.Media.Brushes.Yellow),new Point(5,5));*/

            //Buscando coincidencias con Letras
            //se obtiene un arreglo como: ["letra", "A"]
            letra = reconocerLetra(mano_1, mano_2);
            
            //Si encontró una coincidencia
            if(letra!="NONE")
            {
                pivote = letra.Split(' ');
                if(anterior=="NONE" || anterior!=pivote[1])
                {
                    //Guardar letra para comparación                    
                    anterior = pivote[1];
                    //Obtenemos los segundos de la hora local
                    hora_inicio = DateTime.Now;
                }

                //Obtenemos los segundos de la hora local
                DateTime hora_fin = DateTime.Now;

                

                //Calcular diferencia de segundos entre horas
                TimeSpan duracion = hora_fin - hora_inicio;
                double segundos_totales = duracion.TotalSeconds;
                int segundos = duracion.Seconds;
                if(segundos + 1 >= 3)
                {
                    //Guardar en la cadena para Inteligencia
                    cadena += anterior;
                    //envia cadena a inteligencia
                    //hace una pausa esperando respuesta
                    //recibe cadena de respueta
                    //determina si continua concatenando o hace otro
                        //Si es un mensaje aceptable, mostrar la respuesta

                    letras.Text = cadena + "\n" + StartClient(cadena) + "\n"+palabra_BD(cadena);
                    anterior = "";
                }
            }

   
            
            // Render Joints
            foreach (Joint joint in skeleton.Joints)
            {
                Brush drawBrush = null;

                if (joint.TrackingState == JointTrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;                    
                }
                else if (joint.TrackingState == JointTrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;                    
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, this.SkeletonPointToScreen(joint.Position), JointThickness, JointThickness);
                }
            }
        }


        //funcion de reconocimiento
        private string reconocerLetra(string mano_1, string mano_2)
        {
            string pose = "NONE";
            
            String consulta = "SELECT * FROM imagenes";
            MySqlDataAdapter adaptador = new MySqlDataAdapter(consulta, conexion);
            DataTable data = new DataTable();            
            adaptador.Fill(data);

            string m_compara_1 = "";
            string m_compara_2 = "";

            for (int i = 0; i < data.Rows.Count; i++)
            {
                m_compara_1 = data.Rows[i][2].ToString();
                m_compara_2 = data.Rows[i][3].ToString();

                if (String.Equals(mano_1, m_compara_1) && 
                    String.Equals(mano_2, m_compara_2))
                {
                    pose = data.Rows[i][1] + "";
                }
            }            

            //letras.Text = pose;
            return pose;
        }

        private string StartClient(string cadena)
        {
            // Data buffer for incoming data.
            byte[] bytes = new byte[1024];
            string msj_recibido = "NONE";

            // Connect to a remote device.
            try
            {
                // Establish the remote endpoint for the socket.
                // This example uses port 11000 on the local computer.
                string host = "localhost";
                IPHostEntry ipHostInfo = Dns.Resolve(host);
                IPAddress ipAddress = ipHostInfo.AddressList[0];
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, 5000);

                // Create a TCP/IP  socket.
                Socket sender = new Socket(AddressFamily.InterNetwork,
                    SocketType.Stream, ProtocolType.Tcp);

                // Connect the socket to the remote endpoint. Catch any errors.
                try
                {
                    sender.Connect(remoteEP);

                    Console.WriteLine("Socket connected to {0}",
                        sender.RemoteEndPoint.ToString());

                    // Encode the data string into a byte array.
                    byte[] msg = Encoding.ASCII.GetBytes(cadena.ToLower());//colocar mensaje a enviar

                    // Send the data through the socket.
                    int bytesSent = sender.Send(msg);

                    // Receive the response from the remote device.
                    int bytesRec = sender.Receive(bytes);
                    msj_recibido = Encoding.ASCII.GetString(bytes, 0, bytesRec);

                    // Release the socket.
                    sender.Shutdown(SocketShutdown.Both);
                    sender.Close();

                }
                catch (ArgumentNullException ane)
                {
                    Console.WriteLine("ArgumentNullException : {0}", ane.ToString());
                }
                catch (SocketException se)
                {
                    Console.WriteLine("SocketException : {0}", se.ToString());
                }
                catch (Exception e)
                {
                    Console.WriteLine("Unexpected exception : {0}", e.ToString());
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            return msj_recibido;
        }

        //
        private string mensajeObtenido(string cadena)
        {
            string mensaje = "Not Match";
            //La Inteligencia buscará una coincidencia de la cadena recibida
            //en la base de datos para saber si corresponde a una palabra inglesa

            //Regresa la palabra encontrada o un default porque no encontró algo
            return mensaje;
        }

        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = this.sensor.MapSkeletonPointToDepth(
                                                                             skelpoint,
                                                                             DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }


        private  string palabra_BD(string cadena)
        {
            string mensaje= "NONE";
            String consulta = "SELECT * FROM lexico";
            MySqlDataAdapter adaptador = new MySqlDataAdapter(consulta, conexion);
            DataTable data = new DataTable();
            adaptador.Fill(data);
            bool concidencia = false;
            string palabra_BD;
            for (int i = 0; i < data.Rows.Count; i++)
            {
                palabra_BD = data.Rows[i][1].ToString();
                if (String.Equals(cadena, palabra_BD))
                {
                    concidencia = true;
                }
                
          if(concidencia==true)
        {
            mensaje = "esta palabra ya la conosco";
        }
          else
          {
              mensaje ="esta palabra no la conosco pero ya la aprendi";
          }
            }

            return mensaje;

        }

        /// <summary>
        /// Draws a bone line between two joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw bones from</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="jointType0">joint to start drawing from</param>
        /// <param name="jointType1">joint to end drawing at</param>
        private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1)
        {
            Joint joint0 = skeleton.Joints[jointType0];
            Joint joint1 = skeleton.Joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == JointTrackingState.NotTracked ||
                joint1.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }

            // Don't draw if both points are inferred
            if (joint0.TrackingState == JointTrackingState.Inferred &&
                joint1.TrackingState == JointTrackingState.Inferred)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked)
            {
                drawPen = this.trackedBonePen;
            }

            drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));            

        }





        /// <summary>
        /// Handles the checking or unchecking of the seated mode combo box
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
     /*   private void CheckBoxSeatedModeChanged(object sender, RoutedEventArgs e)
        {
            if (null != this.sensor)
            {
                if (this.checkBoxSeatedMode.IsChecked.GetValueOrDefault())
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                }
                else
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;
                }
            }
        }*/
    }
}