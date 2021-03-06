﻿namespace KDVisuals
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Timers;
    using KDSharp.KDTree;
    using KDSharp.DistanceFunctions;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Timer Resolution for Speed Acceleration.
        /// </summary>
        const int TimerSecondResolution = 10000000 * 2;
        
        /// <summary>
        /// Lock Object for KDTree Editing.
        /// </summary>
        object LockSync = new object();
        
        /// <summary>
        /// KD-Tree which stores the points.
        /// </summary>
        KDTree<EllipseWrapper> Tree;

        /// <summary>
        /// Bitmap which renders them quickly.
        /// </summary>
        WriteableBitmap Bitmap;
        
        /// <summary>
        /// Number of Neighbours to search.
        /// </summary>
        int NumNeighbours;
        
        /// <summary>
        /// Distance Threshold for Nearest Neighbours.
        /// </summary>
        double DistThreshold;
        
        /// <summary>
        /// Mouse Position used as source point for searches.
        /// </summary>
        Point MousePosition;
        
        /// <summary>
        /// Random Object to extract Points Values
        /// </summary>
        Random Random = new Random();
        
        /// <summary>
        /// Timer Triggering Movement Change
        /// </summary>
        Timer MoveChange;
        
        /// <summary>
        /// Retrieve a Random Speed adjusted for TimerResolution.
        /// </summary>
        double RandomSpeed { get { return (Random.NextDouble() - 0.5) / TimerSecondResolution; } }
        
        /// <summary>
        /// Enable Grid Drawing.
        /// </summary>
        bool GridEnabled;
        
        /// <summary>
        /// A data item which is stored in each kd node.
        /// </summary>
        class EllipseWrapper
        {
            public double x, y, speedx, speedy, start;

            public EllipseWrapper(double x, double y)
            {
                this.x = x;
                this.y = y;
            }

            public EllipseWrapper(double x, double y, double speedx, double speedy, double start)
                : this(x, y)
            {
                this.speedx = speedx;
                this.speedy = speedy;
                this.start = start;
            }
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
        }
        
        /// <summary>
        /// On Window Created Attach to Render Event.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            CompositionTarget.Rendering += this.OnUpdate;
        }
        
        /// <summary>
        /// On Window Destoyed Dettach from Render Event.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            CompositionTarget.Rendering -= this.OnUpdate;
        }
        
        /// <summary>
        /// On Size Changed Regen Canvas Bitmap
        /// </summary>
        /// <param name="sizeInfo"></param>
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            RegenBitmap();
        }
        
        /// <summary>
        /// On Render Update, Redraw Tree.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnUpdate(object sender, object e)
        {
            DrawTree();
        }
        
        /// <summary>
        /// Recreate Bitmap Image for Drawing.
        /// </summary>
        void RegenBitmap()
        {
            lock (LockSync)
            {
                Bitmap = BitmapFactory.New((int)cnvPoints.ActualWidth, (int)cnvPoints.ActualHeight);
                var pBrush = new ImageBrush();
                pBrush.ImageSource = Bitmap;
                cnvPoints.Background = pBrush;
                
                // Clear the bitmap to light blue.
                using (Bitmap.GetBitmapContext())
                    Bitmap.Clear(Colors.LightBlue);
                
                // Clear Canvas
                cnvPoints.Children.Clear();
            }
        }
        
        /// <summary>
        /// Draw Tree Points.
        /// </summary>
        void DrawTree()
        {
            // Bail if nothing to draw on.
            if (Bitmap == null || Tree == null)
                return;
            
            lock (LockSync)
            {
                RegenUIValues();
                var cnvWidth = cnvPoints.ActualWidth;
                var cnvHeight = cnvPoints.ActualHeight;
                
                // Get Squared Threshold if not negative
                var threshold = DistThreshold / cnvWidth;
                if (DistThreshold >= 0.0)
                    threshold *= threshold;
                    
                // Get the drawing context.
                using (Bitmap.GetBitmapContext())
                {
                    // Clean Up
                    Bitmap.Clear(Colors.LightBlue);
                    
                    DrawGrid(cnvWidth, cnvHeight);
                    
                    // Match Neighbours
                    var matched = Tree.Dimensions > 2
                        ? Tree.NearestNeighbors(new [] { MousePosition.X / cnvWidth, MousePosition.Y / cnvHeight, 0.0, 0.0, 0.0 }, NumNeighbours, threshold).ToArray()
                        : Tree.NearestNeighbors(new [] { MousePosition.X / cnvWidth, MousePosition.Y / cnvHeight }, NumNeighbours, threshold).ToArray();
                    
                    foreach (var nomatch in Tree.Except(matched))
                    {
                        var positionX = nomatch.x;
                        var positionY = nomatch.y;
                        
                        if (Tree.Dimensions > 2)
                        {
                            var elapsed = DateTime.UtcNow.Ticks - nomatch.start;
                            positionX += nomatch.speedx * elapsed;
                            positionY += nomatch.speedy * elapsed;
                        }
                        
                        if (positionX > 1.0 || positionX < 0.0 || positionY > 1.0 || positionY < 0.0)
                            continue;
                        
                       Bitmap.DrawEllipse((int)(positionX * cnvWidth - 2), (int)(positionY * cnvHeight - 2), (int)(positionX * cnvWidth + 2), (int)(positionY * cnvHeight + 2), Colors.Orange);
                    }
                    
                    foreach (var match in matched)
                    {
                        var positionX = match.x;
                        var positionY = match.y;
                        
                        if (Tree.Dimensions > 2)
                        {
                            var elapsed = DateTime.UtcNow.Ticks - match.start;
                            positionX += match.speedx * elapsed;
                            positionY += match.speedy * elapsed;
                        }
                        
                        if (positionX > 1.0 || positionX < 0.0 || positionY > 1.0 || positionY < 0.0)
                            continue;
                        
                        Bitmap.FillEllipse((int)(positionX * cnvWidth - 2), (int)(positionY * cnvHeight - 2), (int)(positionX * cnvWidth + 2), (int)(positionY * cnvHeight + 2), Colors.Red);
                        Bitmap.DrawEllipse((int)(positionX * cnvWidth - 2), (int)(positionY * cnvHeight - 2), (int)(positionX * cnvWidth + 2), (int)(positionY * cnvHeight + 2), Colors.Green);
                    }
                    // Draw Cursor
                    Bitmap.DrawEllipse((int)(MousePosition.X - DistThreshold), (int)(MousePosition.Y - (DistThreshold / cnvWidth) * cnvHeight), (int)(MousePosition.X + DistThreshold), (int)(MousePosition.Y + (DistThreshold / cnvWidth) * cnvHeight), Colors.Black);
                }
            }
        }
        
        /// <summary>
        /// Draw Tree Grid if Enabled.
        /// </summary>
        /// <param name="cnvWidth"></param>
        /// <param name="cnvHeight"></param>
        void DrawGrid(double cnvWidth, double cnvHeight)
        {
            if (GridEnabled)
            {
                var stack = new Stack<KDTreeNode>();
                stack.Push(Tree.RootNode);
                var colors = new Color[] { Colors.Purple, Colors.Brown, Colors.Black };
                var count = 0;
                while(stack.Count > 0)
                {
                    var current = stack.Pop();
                       var minimumBound = current.MinimumBound;
                    var maximumBound = current.MaximumBound;
                    
                    if (minimumBound != null && maximumBound != null)
                    {
                        if (Tree.Dimensions > 2)
                        {
                            var elapsedmin = DateTime.UtcNow.Ticks - minimumBound[4];
                            var minx = (minimumBound[0] + minimumBound[2] * elapsedmin) * cnvWidth;
                            var miny = (minimumBound[1] + minimumBound[3] * elapsedmin) * cnvHeight;
                            
                            var elapsedmax = DateTime.UtcNow.Ticks - maximumBound[4];
                            var maxx = (maximumBound[0] + maximumBound[2] * elapsedmax) * cnvWidth;
                            var maxy = (maximumBound[1] + maximumBound[3] * elapsedmax) * cnvHeight;
                            
                            Bitmap.DrawRectangle((int)minx, (int)miny, (int)maxx, (int)maxy, colors[count%colors.Length]);
                        }
                        else
                        {
                            var minx = minimumBound[0] * cnvWidth;
                            var miny = minimumBound[1] * cnvHeight;
                            var maxx = maximumBound[0] * cnvWidth;
                            var maxy = maximumBound[1] * cnvHeight;
                            
                            Bitmap.DrawRectangle((int)minx, (int)miny, (int)maxx, (int)maxy, colors[count%colors.Length]);
                        }
                    }
                    
                    if (!current.IsLeaf)
                    {
                        stack.Push(current.Right);
                        stack.Push(current.Left);
                    }
                    
                    ++count;
                }
            }
        }
        
        /// <summary>
        /// Gather UI Values for Nearest Neighbours Searches.
        /// </summary>
        void RegenUIValues()
        {
            txtFindMax.Foreground = Brushes.Black;
            if (!int.TryParse(txtFindMax.Text, out NumNeighbours))
                txtFindMax.Foreground = Brushes.Red;

            txtFindThreshold.Foreground = Brushes.Black;
            if (!double.TryParse(txtFindThreshold.Text, out DistThreshold))
                txtFindThreshold.Foreground = Brushes.Red;
        }

        /// <summary>
        /// Randomise the layout of points.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Button_Click(object sender, RoutedEventArgs e)
        {
            RegenBitmap();
            
            lock (LockSync)
            {
                // Get the number we want to generate and update the UI.
                var iResult = 0;
                
                if (!int.TryParse(txtPoints.Text, out iResult))
                {
                    txtPoints.Foreground = Brushes.Red;
                    return;
                }
                
                if (iResult < 0)
                {
                    txtPoints.Foreground = Brushes.Red;
                    return;
                }
                
                txtPoints.Foreground = Brushes.Black;
                
                // Regen Tree
                if (Tree != null && Tree.Dimensions > 2)
                {
                    Tree = new KDTree<EllipseWrapper>(new SquaredEuclideanDistanceWithTranslation(() => DateTime.UtcNow.Ticks, 2), 5);
                }
                else
                {
                    Tree = new KDTree<EllipseWrapper>(2);
                }
                
                // Add Point to the tree
                var pRandom = new Random();
                for (int i = 0; i < iResult; ++i)
                {
                    // Position it and add it to the canvas.
                    var x = pRandom.NextDouble();
                    var y = pRandom.NextDouble();
                    if (Tree.Dimensions > 2)
                    {
                        var speedx = RandomSpeed;
                        var speedy = RandomSpeed;
                        var time = DateTime.UtcNow.Ticks;
                        Tree.AddPoint(new [] { x, y, speedx, speedy, time }, new EllipseWrapper(x, y, speedx, speedy, time));
                    }
                    else
                    {
                        Tree.AddPoint(new [] { x, y }, new EllipseWrapper(x, y));
                    }
                }
            }
        }
        
        /// <summary>
        /// Randomize Moving Points
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Move_Click(object sender, RoutedEventArgs e)
        {
            // Bail if no tree...
            if (Tree == null)
                return;
            
            RegenBitmap();
            
            lock (LockSync)
            {
                if (MoveChange != null)
                {
                    MoveChange.Stop();
                    MoveChange = null;
                }
                
                if (Tree.Dimensions > 2)
                {
                    var data = Tree.ToArray();
                    Tree = new KDTree<EllipseWrapper>(2);
                    foreach (var item in data)
                    {
                        var elapsed = DateTime.UtcNow.Ticks - item.start;
                        var nomove = new EllipseWrapper(item.x + item.speedx * elapsed, item.y + item.speedy * elapsed);
                        Tree.AddPoint(new [] { nomove.x, nomove.y }, nomove);
                    }
                    moveButton.Content = "Move";
                }
                else
                {
                    var data = Tree.ToArray();
                    Tree = new KDTree<EllipseWrapper>(new SquaredEuclideanDistanceWithTranslation(() => DateTime.UtcNow.Ticks, 2), 5);
                    
                    var rand = new Random();
                    foreach (var item in data)
                    {
                        var speedx = RandomSpeed;
                        var speedy = RandomSpeed;
                        var move = new EllipseWrapper(item.x, item.y, speedx, speedy, DateTime.UtcNow.Ticks);
                        Tree.AddPoint(new [] { move.x, move.y, move.speedx, move.speedy, move.start }, move);
                    }
                    
                    MoveChange = new Timer();
                    MoveChange.AutoReset = false;
                    MoveChange.Elapsed += (object sndr, ElapsedEventArgs ev) => ChangeMovement();
                    MoveChange.Interval = 1000;
                    MoveChange.Start();
                    
                    moveButton.Content = "Stop";
                }
            }
        }
        
        /// <summary>
        /// Update Points Movements Periodically.
        /// </summary>
        void ChangeMovement()
        {
            KDTree<EllipseWrapper> currentTree;
            EllipseWrapper[] items;
            
            lock (LockSync)
            {
                currentTree = Tree;
                items = Tree.ToArray();
                if (Tree.Dimensions < 3)
                    return;
            }
            
            foreach (var item in items)
            {
                lock (LockSync)
                {
                    if (currentTree != Tree)
                        break;
                    
                    var elapsed = DateTime.UtcNow.Ticks - item.start;
                    item.x += item.speedx * elapsed;
                    item.y += item.speedy * elapsed;
                    
                    if (item.x < 0.0 || item.x > 1.0 || item.y < 0.0 || item.y > 1.0)
                    {
                        var dx = item.x - 0.5;
                        var dy = item.y - 0.5;
                        var norm = Math.Sqrt(dx*dx + dy*dy);
                        
                        item.speedx = - (dx / norm / TimerSecondResolution / 1.5);
                        item.speedy = - (dy / norm / TimerSecondResolution / 1.5);
                    }
                    else
                    {
                        item.speedx = RandomSpeed;
                        item.speedy = RandomSpeed;
                    }
                    
                    item.start = DateTime.UtcNow.Ticks;
                    Tree.MovePoint(new [] { item.x, item.y, item.speedx, item.speedy, item.start }, item);
                }
            }
            
            lock (LockSync)
            {
                if (Tree.RemovalCount > 2 * Tree.Count)
                    Tree.Regen();
                
                MoveChange.Interval = 1000;
            }
        }

        /// <summary>
        /// Capture Grid Show Click Events.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Grid_Click(object sender, RoutedEventArgs e)
        {
            GridEnabled = !GridEnabled;
            
            gridButton.Content = GridEnabled ? "Hide" : "Show";
        }
        
        /// <summary>
        /// When the mouse is moved, highlight the nearby nodes.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void cnvPoints_MouseMove(object sender, MouseEventArgs e)
        {
            // Get Current MousePoint
            MousePosition = e.GetPosition(cnvPoints);
        }
    }
}
