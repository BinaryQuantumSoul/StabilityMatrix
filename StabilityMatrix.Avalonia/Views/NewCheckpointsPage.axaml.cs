﻿using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.ViewModels;
using StabilityMatrix.Avalonia.ViewModels.CheckpointManager;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.FileInterfaces;

namespace StabilityMatrix.Avalonia.Views;

[Singleton]
public partial class NewCheckpointsPage : UserControlBase
{
    private Dictionary<CheckpointCategory, DispatcherTimer> dragTimers = new();

    public NewCheckpointsPage()
    {
        InitializeComponent();

        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragLeaveEvent, OnDragExit);
    }

    private void OnDragExit(object? sender, DragEventArgs e)
    {
        if (e.Source is not Control control)
            return;

        switch (control)
        {
            case TreeViewItem treeViewItem:
                treeViewItem.Classes.Remove("dragover");
                break;
            case Border border:
                border.Classes.Remove("dragover");
                break;
            case TextBlock textBlock:
                textBlock.Parent?.Classes.Remove("dragover");
                break;
        }

        var sourceDataContext = control switch
        {
            TreeViewItem treeView => treeView.DataContext,
            Border border => border.Parent?.Parent?.DataContext,
            TextBlock textBlock => textBlock.Parent?.DataContext,
            _ => null
        };

        if (sourceDataContext is not CheckpointCategory category)
            return;

        if (!dragTimers.TryGetValue(category, out var timer))
            return;

        timer.Stop();
        dragTimers.Remove(category);
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Source is not Control control)
            return;

        switch (control)
        {
            case TreeViewItem treeViewItem:
                treeViewItem.Classes.Add("dragover");
                break;
            case Border border:
                border.Classes.Add("dragover");
                break;
            case TextBlock textBlock:
                textBlock.Parent?.Classes.Add("dragover");
                break;
        }

        var sourceDataContext = control switch
        {
            TreeViewItem treeView => treeView.DataContext,
            Border border => border.Parent?.Parent?.DataContext,
            TextBlock textBlock => textBlock.Parent?.DataContext,
            _ => null
        };

        if (sourceDataContext is not CheckpointCategory category)
            return;

        if (dragTimers.TryGetValue(category, out var timer))
        {
            timer.Stop();
            timer.Start();
        }
        else
        {
            var newTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500),
                IsEnabled = true
            };
            newTimer.Tick += (timerObj, _) =>
            {
                var treeViewItem = control switch
                {
                    TreeViewItem treeView => treeView,
                    Border border => border.Parent?.Parent as TreeViewItem,
                    TextBlock textBlock => textBlock.Parent as TreeViewItem,
                    _ => null
                };

                if (treeViewItem != null && category.SubDirectories.Count > 0)
                {
                    treeViewItem.IsExpanded = true;
                }

                (timerObj as DispatcherTimer)?.Stop();
                dragTimers.Remove(category);
            };
            dragTimers.Add(category, newTimer);
        }
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        var sourceDataContext = (e.Source as Control)?.DataContext;
        if (DataContext as NewCheckpointsPageViewModel is not { SelectedCategory: not null } checkpointsVm)
        {
            return;
        }

        switch (sourceDataContext)
        {
            case CheckpointCategory category:
                if (e.Data.GetContext<CheckpointFileViewModel>() is { } vm)
                {
                    await checkpointsVm.MoveBetweenFolders(vm.CheckpointFile, category.Path);
                }
                else if (e.Data.Get(DataFormats.Files) is IEnumerable<IStorageItem> files)
                {
                    var paths = files.Select(f => f.Path.LocalPath).ToArray();
                    await checkpointsVm.ImportFilesAsync(paths, category.Path);
                }
                break;
            case CheckpointFileViewModel fileViewModel:
                if (e.Data.GetContext<CheckpointFileViewModel>() is { } fileVm)
                {
                    await checkpointsVm.MoveBetweenFolders(
                        fileVm.CheckpointFile,
                        checkpointsVm.SelectedCategory.Path
                    );
                }
                else if (e.Data.Get(DataFormats.Files) is IEnumerable<IStorageItem> files)
                {
                    var paths = files.Select(f => f.Path.LocalPath).ToArray();
                    await checkpointsVm.ImportFilesAsync(paths, checkpointsVm.SelectedCategory.Path);
                }
                break;
            case CheckpointFolder folder:
            {
                if (e.Data.GetContext<CheckpointFile>() is not { } file)
                {
                    await folder.OnDrop(e);
                    break;
                }

                var filePath = new FilePath(file.FilePath);
                if (filePath.Directory?.FullPath != folder.DirectoryPath)
                {
                    await folder.OnDrop(e);
                }
                break;
            }
            case CheckpointFile file:
            {
                if (e.Data.GetContext<CheckpointFile>() is not { } dragFile)
                {
                    await file.ParentFolder.OnDrop(e);
                    break;
                }

                var parentFolder = file.ParentFolder;
                var dragFilePath = new FilePath(dragFile.FilePath);
                if (dragFilePath.Directory?.FullPath != parentFolder.DirectoryPath)
                {
                    await parentFolder.OnDrop(e);
                }
                break;
            }
        }
    }
}
