using System;
using System.IO;
using System.Net;
using System.Security;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Resources;

namespace GifImageLib
{
    public class GifImage : UserControl
    {
        private GifAnimation _gifAnimation;
        private Image _image;

        public static readonly DependencyProperty ForceGifAnimProperty = DependencyProperty.Register("ForceGifAnim", typeof(bool), typeof(GifImage), new FrameworkPropertyMetadata(false));
        public bool ForceGifAnim
        {
            get => (bool)GetValue(ForceGifAnimProperty);
            set => SetValue(ForceGifAnimProperty, value);
        }

        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register("Source", typeof(string), typeof(GifImage), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnSourceChanged));
        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            GifImage obj = (GifImage)d;
            string s = (string)e.NewValue;
            obj.CreateFromSourceString(s);
        }
        public string Source
        {
            get => (string)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }


        public static readonly DependencyProperty StretchProperty = DependencyProperty.Register("Stretch", typeof(Stretch), typeof(GifImage), new FrameworkPropertyMetadata(Stretch.Fill, FrameworkPropertyMetadataOptions.AffectsMeasure, OnStretchChanged));
        private static void OnStretchChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            GifImage obj = (GifImage)d;
            Stretch s = (Stretch)e.NewValue;
            if (obj._gifAnimation != null)
            {
                obj._gifAnimation.Stretch = s;
            }
            else if (obj._image != null)
            {
                obj._image.Stretch = s;
            }
        }
        public Stretch Stretch
        {
            get => (Stretch)GetValue(StretchProperty);
            set => SetValue(StretchProperty, value);
        }

        public static readonly DependencyProperty StretchDirectionProperty = DependencyProperty.Register("StretchDirection", typeof(StretchDirection), typeof(GifImage), new FrameworkPropertyMetadata(StretchDirection.Both, FrameworkPropertyMetadataOptions.AffectsMeasure, OnStretchDirectionChanged));
        private static void OnStretchDirectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            GifImage obj = (GifImage)d;
            StretchDirection s = (StretchDirection)e.NewValue;
            if (obj._gifAnimation != null)
            {
                obj._gifAnimation.StretchDirection = s;
            }
            else if (obj._image != null)
            {
                obj._image.StretchDirection = s;
            }
        }
        public StretchDirection StretchDirection
        {
            get => (StretchDirection)GetValue(StretchDirectionProperty);
            set => SetValue(StretchDirectionProperty, value);
        }

        public delegate void ExceptionRoutedEventHandler(object sender, GifImageExceptionRoutedEventArgs args);

        public static readonly RoutedEvent ImageFailedEvent = EventManager.RegisterRoutedEvent("ImageFailed", RoutingStrategy.Bubble, typeof(ExceptionRoutedEventHandler), typeof(GifImage));

        void image_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            RaiseImageFailedEvent(e.ErrorException);
        }


        void RaiseImageFailedEvent(Exception exp)
        {
            GifImageExceptionRoutedEventArgs newArgs =
                new GifImageExceptionRoutedEventArgs(ImageFailedEvent, this) {ErrorException = exp};
            RaiseEvent(newArgs);
        }


        private void DeletePreviousImage()
        {
            if (_image != null)
            {
                RemoveLogicalChild(_image);
                _image = null;
            }
            if (_gifAnimation != null)
            {
                RemoveLogicalChild(_gifAnimation);
                _gifAnimation = null;
            }
        }

        private void CreateNonGifAnimationImage()
        {
            _image = new Image();
            _image.ImageFailed += image_ImageFailed;
            ImageSource src = (ImageSource)(new ImageSourceConverter().ConvertFromString(Source));
            _image.Source = src;
            _image.Stretch = Stretch;
            _image.StretchDirection = StretchDirection;
            AddChild(_image);
        }


        private void CreateGifAnimation(MemoryStream memoryStream)
        {
            _gifAnimation = new GifAnimation();
            _gifAnimation.CreateGifAnimation(memoryStream);
            _gifAnimation.Stretch = Stretch;
            _gifAnimation.StretchDirection = StretchDirection;
            AddChild(_gifAnimation);
        }


        private void CreateFromSourceString(string source)
        {
            DeletePreviousImage();
            Uri uri;

            try
            {
                uri = new Uri(source, UriKind.RelativeOrAbsolute);
            }
            catch (Exception exp)
            {
                RaiseImageFailedEvent(exp);
                return;
            }

            if (source.Trim().ToUpper().EndsWith(".GIF") || ForceGifAnim)
            {
                if (!uri.IsAbsoluteUri)
                {
                    GetGifStreamFromPack(uri);
                }
                else
                {

                    string leftPart = uri.GetLeftPart(UriPartial.Scheme);

                    if (leftPart == "http://" || leftPart == "ftp://" || leftPart == "file://")
                    {
                        GetGifStreamFromHttp(uri);
                    }
                    else if (leftPart == "pack://")
                    {
                        GetGifStreamFromPack(uri);
                    }
                    else
                    {
                        CreateNonGifAnimationImage();
                    }
                }
            }
            else
            {
                CreateNonGifAnimationImage();
            }
        }

        private delegate void WebRequestFinishedDelegate(MemoryStream memoryStream);

        private void WebRequestFinished(MemoryStream memoryStream)
        {
            CreateGifAnimation(memoryStream);
        }

        private delegate void WebRequestErrorDelegate(Exception exp);

        private void WebRequestError(Exception exp)
        {
            RaiseImageFailedEvent(exp);
        }

        private void WebResponseCallback(IAsyncResult asyncResult)
        {
            WebReadState webReadState = (WebReadState)asyncResult.AsyncState;
            try
            {
                var webResponse = webReadState.WebRequest.EndGetResponse(asyncResult);
                webReadState.ReadStream = webResponse.GetResponseStream();
                webReadState.Buffer = new byte[100000];
                webReadState.ReadStream?.BeginRead(webReadState.Buffer, 0, webReadState.Buffer.Length,
                                                   WebReadCallback, webReadState);
            }
            catch (WebException exp)
            {
                Dispatcher.Invoke(DispatcherPriority.Render, new WebRequestErrorDelegate(WebRequestError), exp);
            }
        }

        private void WebReadCallback(IAsyncResult asyncResult)
        {
            WebReadState webReadState = (WebReadState)asyncResult.AsyncState;
            int count = webReadState.ReadStream.EndRead(asyncResult);
            if (count > 0)
            {
                webReadState.MemoryStream.Write(webReadState.Buffer, 0, count);
                try
                {
                    webReadState.ReadStream.BeginRead(webReadState.Buffer, 0, webReadState.Buffer.Length, WebReadCallback, webReadState);
                }
                catch (WebException exp)
                {
                    Dispatcher.Invoke(DispatcherPriority.Render, new WebRequestErrorDelegate(WebRequestError), exp);
                }
            }
            else
            {
                Dispatcher.Invoke(DispatcherPriority.Render, new WebRequestFinishedDelegate(WebRequestFinished), webReadState.MemoryStream);
            }
        }

        private void GetGifStreamFromHttp(Uri uri)
        {
            try
            {
                WebReadState webReadState = new WebReadState
                {
                    MemoryStream = new MemoryStream(),
                    WebRequest   = WebRequest.Create(uri)
                };
                webReadState.WebRequest.Timeout = 10000;

                webReadState.WebRequest.BeginGetResponse(WebResponseCallback, webReadState);
            }
            catch (SecurityException)
            {
                CreateNonGifAnimationImage();
            }
        }


        private void ReadGifStreamSynch(Stream s)
        {
            MemoryStream memoryStream;
            using (s)
            {
                memoryStream = new MemoryStream((int)s.Length);
                BinaryReader br = new BinaryReader(s);
                var gifData = br.ReadBytes((int)s.Length);
                memoryStream.Write(gifData, 0, (int)s.Length);
                memoryStream.Flush();
            }
            CreateGifAnimation(memoryStream);
        }

        private void GetGifStreamFromPack(Uri uri)
        {
            try
            {
                StreamResourceInfo streamInfo;

                if (!uri.IsAbsoluteUri)
                {
                    streamInfo = Application.GetContentStream(uri) ?? Application.GetResourceStream(uri);
                }
                else
                {
                    if (uri.GetLeftPart(UriPartial.Authority).Contains("siteoforigin"))
                    {
                        streamInfo = Application.GetRemoteStream(uri);
                    }
                    else
                    {
                        streamInfo = Application.GetContentStream(uri) ?? Application.GetResourceStream(uri);
                    }
                }
                if (streamInfo == null)
                {
                    throw new FileNotFoundException("Resource not found.", uri.ToString());
                }
                ReadGifStreamSynch(streamInfo.Stream);
            }
            catch (Exception exp)
            {
                RaiseImageFailedEvent(exp);
            }
        }
    }
}
