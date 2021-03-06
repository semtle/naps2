﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using NAPS2.Lang.Resources;
using NAPS2.Operation;
using NAPS2.Scan.Images;
using NAPS2.Scan.Images.Transforms;
using NAPS2.Util;

namespace NAPS2.Scan.Images
{
    public class DeskewOperation : OperationBase
    {
        private readonly ThreadFactory threadFactory;
        private readonly ThumbnailRenderer thumbnailRenderer;

        private bool cancel;
        private Thread thread;

        public DeskewOperation(ThreadFactory threadFactory, ThumbnailRenderer thumbnailRenderer)
        {
            this.threadFactory = threadFactory;
            this.thumbnailRenderer = thumbnailRenderer;

            AllowCancel = true;
        }

        public bool Start(ICollection<ScannedImage> images)
        {
            ProgressTitle = MiscResources.AutoDeskewProgress;
            Status = new OperationStatus
            {
                StatusText = MiscResources.AutoDeskewing,
                MaxProgress = images.Count
            };
            cancel = false;

            thread = threadFactory.StartThread(() =>
            {
                Pipeline.For(images).StepParallel(img =>
                {
                    if (cancel)
                    {
                        return null;
                    }
                    Bitmap bitmap = img.GetImage();
                    try
                    {
                        if (cancel)
                        {
                            return null;
                        }
                        var transform = RotationTransform.Auto(bitmap);
                        if (cancel)
                        {
                            return null;
                        }
                        bitmap = transform.Perform(bitmap);
                        img.SetThumbnail(thumbnailRenderer.RenderThumbnail(bitmap));
                        return Tuple.Create(img, transform);
                    }
                    finally
                    {
                        bitmap.Dispose();
                    }
                }).Step((img, transform) =>
                {
                    img.AddTransform(transform);
                    Status.CurrentProgress++;
                    InvokeStatusChanged();
                    return img;
                }).Run();
                Status.Success = !cancel;
                InvokeFinished();
            });

            return true;
        }

        public override void Cancel()
        {
            cancel = true;
        }

        public override void WaitUntilFinished()
        {
            thread.Join();
        }
    }
}
