﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.XPath;
using System.Diagnostics;
using System.Xml;
using Newtonsoft.Json;
using Validation.mvdXML;
using Xbim.COBieLite;
using Xbim.Ifc2x3.Extensions;
using Xbim.IO.GroupingAndStyling;
using XbimXplorer.Plugins.DPoWValidation.ValidationObjects;
using XbimXplorer.PluginSystem;
using XbimXplorer;
using Xbim.XbimExtensions.Interfaces;
using System.IO;
using Xbim.IO;
using Validation.ValidationExtensions;
using Xbim.Presentation;
using Microsoft.Win32;
using System.ComponentModel;
using Xbim.Ifc2x3.PropertyResource;
using Xbim.Ifc2x3.MeasureResource;
using Xbim.Ifc2x3.Kernel;
using System.Xml.Serialization;
using Validation.MV;
using Validation.ValidationObjects;
using Path = System.IO.Path;
using XbimXplorer.Plugins.DPoWValidation;

namespace Validation
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : UserControl, xBimXplorerPluginWindow 
    {
        public MainWindow()
        {
            InitializeComponent();
            IsFileOpen = false;
        }
        // xml navigation sample at http://support.microsoft.com/kb/308333

        internal MvdXMLDocument Doc = null;

        private void OpenFile(object sender, RoutedEventArgs e)
        {
            var openFile = new OpenFileDialog();
            openFile.Filter = @"All supprted files|*.xml;*.json|CobieLite XML|*.xml|CobieLite Json|*.json";
            var res = openFile.ShowDialog();

            if (res.HasValue && res.Value)
            {
                // Model Deserialisation 
                //
                var cobieModelFileInfo = new FileInfo(openFile.FileName);

                FacilityType t = null;
                try
                {
                    switch (cobieModelFileInfo.Extension.ToLowerInvariant())
                    {
                        case ".xml":
                            var x = FacilityType.GetSerializer();
                            var reader = new XmlTextReader(cobieModelFileInfo.FullName);
                            t = (FacilityType)x.Deserialize(reader);
                            reader.Close();
                            break;
                        case ".json":
                            var data = File.ReadAllText(cobieModelFileInfo.FullName);
                            t = JsonConvert.DeserializeObject<FacilityType>(data);
                            break;
                    }
                }
                catch (Exception)
                {
                }
                if (t != null)
                {
                    SetFacility(t);
                }
            }
        }

        private void SetFacility(FacilityType facility)
        {
            ReqFacility = facility;
            var assets = new List<AssetTypeInfoTypeVM>();
            var spaces = new List<SpaceTypeVM>();

            foreach (var asset in ReqFacility.AssetTypes.AssetType)
            {
                assets.Add(new AssetTypeInfoTypeVM(asset));
            }
            lstClassifications.ItemsSource = assets;
            lstSpaces.ItemsSource = spaces;
            IsFileOpen = true;
        }

        private bool IsFileOpen
        {
            get
            {
                return (Doc != null);
            }
            set
            {
                if (value == true)
                {
                    UI.Visibility = System.Windows.Visibility.Visible;
                    OpenButton.Visibility = System.Windows.Visibility.Hidden;
                }
                else
                {
                    UI.Visibility = System.Windows.Visibility.Hidden;
                    OpenButton.Visibility = System.Windows.Visibility.Visible;
                }
                // PropertyChanged.Invoke(this, new PropertyChangedEventArgs("OpenButtonVisibility"));
                // PropertyChanged.Invoke(this, new PropertyChangedEventArgs("UIVisibility"));
            }
        }

        public Visibility OpenButtonVisibility { get { return (IsFileOpen) ? System.Windows.Visibility.Hidden : System.Windows.Visibility.Visible; } }
        public Visibility UIVisibility { get { return (!IsFileOpen) ? System.Windows.Visibility.Hidden : System.Windows.Visibility.Visible; } }
        
        private void lstTypes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstClassifications.SelectedItem == null)
                return;
            var selectedType = lstClassifications.SelectedItem.ToString();

            var vm = lstClassifications.SelectedItem as AssetTypeInfoTypeVM;
            
            var rep = new List<ReportResult>();
            if (ModelFacility != null && vm != null)
            {
                var creq = new CobieAssetTypeRequirement(vm.DataModel);
                var sb = new StringBuilder();
                rep.AddRange(creq.Validate(ModelFacility, -1, sb));
                report.Text = sb.ToString();
            }
            lstClassResults.ItemsSource = rep;
        }

        private void docPropsAllConceptRoots(object sender, RoutedEventArgs e)
        {
            report.Text = "";
            foreach (var item in lstClassifications.Items)
            {
                var suitableroots = Doc.GetConceptRoots(item.ToString());
                foreach (var item2 in suitableroots)
                {
                    item2.StringReport();
                }    
            }
            report.Text += Doc.ReportProps();
        }

        private void lstSpaces_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            report.Text = "";
            if (lstSpaces.SelectedItem == null)
                return;

            List<ReportResult> rep = new List<ReportResult>();
                

            LstSpaceResults.ItemsSource = rep;
            
        }


        private XplorerMainWindow xpWindow;

        // ---------------------------
        // plugin system related stuff
        //

        public void BindUI(XplorerMainWindow mainWindow)
        {
            xpWindow = mainWindow;
            this.SetBinding(SelectedItemProperty, new Binding("SelectedItem") { Source = mainWindow, Mode = BindingMode.OneWay });
            this.SetBinding(ModelProperty, new Binding()); // whole datacontext binding, see http://stackoverflow.com/questions/8343928/how-can-i-create-a-binding-in-code-behind-that-doesnt-specify-a-path
        }

        // SelectedEntity
        public IPersistIfcEntity SelectedEntity
        {
            get { return (IPersistIfcEntity)GetValue(SelectedItemProperty); }
            set { SetValue(SelectedItemProperty, value); }
        }

        public static DependencyProperty SelectedItemProperty =
            DependencyProperty.Register("SelectedEntity", typeof(IPersistIfcEntity), typeof(MainWindow), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits,
                                                                      new PropertyChangedCallback(OnSelectedEntityChanged)));

        // Model
        public IModel Model
        {
            get { return (IModel)GetValue(ModelProperty); }
            set { SetValue(ModelProperty, value); }
        }

        public static DependencyProperty ModelProperty =
            DependencyProperty.Register("Model", typeof(IModel), typeof(MainWindow), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits,
                                                                      new PropertyChangedCallback(OnSelectedEntityChanged)));

        internal FacilityType ModelFacility = null;
        internal FacilityType ReqFacility = null;

        
        private static void OnSelectedEntityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // if any UI event should happen it needs to be specified here
            MainWindow ctrl = d as MainWindow;
            if (ctrl != null)
            {
                if (e.Property.Name == "Model")
                {
                    var model = e.NewValue as XbimModel;
                    if (model != null)
                    {
                        try
                        {
                            var helper = new CoBieLiteHelper(model, "UniClass");
                            var facilities = helper.GetFacilities();
                            ctrl.ModelFacility = facilities.FirstOrDefault();
                        }
                        catch (Exception)
                        {
                            ctrl.ModelFacility = null;
                        }
                    }
                    else
                        ctrl.ModelFacility = null;
                }
                else if (e.Property.Name == "SelectedEntity")
                {
                    var selectedEnt = e.NewValue as Xbim.Ifc2x3.Kernel.IfcProduct;
                    if (selectedEnt == null) 
                        return;

                    

                    var type = selectedEnt.GetDefiningType();
                    if (type == null || ctrl.ReqFacility == null) 
                        return;

                    var result = selectedEnt.Validates(ctrl.ReqFacility);


                    var spec =
                        ctrl.ModelFacility.AssetTypes.AssetType.FirstOrDefault(
                            x => x.externalID == type.EntityLabel.ToString());

                    var req =
                        ctrl.ReqFacility.AssetTypes.AssetType.FirstOrDefault(x => spec != null && x.AssetTypeCategory == spec.AssetTypeCategory);

                    if (req != null)
                    {
                        var creq = new CobieAssetTypeRequirement(req);
                        StringBuilder b = new StringBuilder();
                        creq.Validate(ctrl.ModelFacility, selectedEnt.EntityLabel, b);
                        var rep = b.ToString();
                        ctrl.report.Text = rep;
                    }
                }
            }
        }
        
        public string MenuText
        {
            get { return "DPoW Validation"; }
        }

        public string WindowTitle
        {
            get { return "DPoW Validation"; }
        }

        private void ModelInfo(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine(Model != null);
        }

        private void lstClassResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstClassResults.SelectedItem != null && lstClassResults.SelectedItem is ReportResult)
            {
                //EntitySelection s = new EntitySelection();
                //foreach (var item in ret)
                //{
                //    s.Add(Model.Instances[item]);
                //}
                //ParentWindow.DrawingControl.Selection = s;
                int selectedLabel = ((ReportResult)lstClassResults.SelectedItem).EntityLabel;
                xpWindow.SelectedItem = Model.Instances[selectedLabel];
            }
        }

        private void TrafficLight(object sender, RoutedEventArgs e)
        {
            var ls = new TrafficLightStyler((XbimModel)this.Model, this);
            ls.UseAmber = UseAmber;
            xpWindow.DrawingControl.LayerStyler = ls;

            var newLayerStyler = new ValidationResultStyler();
            xpWindow.DrawingControl.GeomSupport2LayerStyler = newLayerStyler;

            xpWindow.DrawingControl.ReloadModel(/*Options: DrawingControl3D.ModelRefreshOptions.ViewPreserveAll*/);
        }


        public PluginWindowDefaultUIShow DefaultUIActivation
        { get { return PluginWindowDefaultUIShow.onLoad; } }

        public PluginWindowDefaultUIContainerEnum DefaultUIContainer
        { get { return PluginWindowDefaultUIContainerEnum.LayoutAnchorable; } }

        private void CloseFile(object sender, RoutedEventArgs e)
        {
            Doc = null;
            lstClassifications.ItemsSource = null;
            lstSpaces.ItemsSource = null;
            IsFileOpen = false;
        }

        private void FixCobieProp(object sender, RoutedEventArgs e)
        {
           

            var atClassifications = new HashSet<string>();

            for (var i = 0; i < ModelFacility.AssetTypes.AssetType.Count; i++)
            {
                string thisCat = ModelFacility.AssetTypes.AssetType[i].AssetTypeCategory;

                if (atClassifications.Contains(thisCat))
                {
                    ModelFacility.AssetTypes.AssetType[i] = null;
                }
                else
                {
                    atClassifications.Add(thisCat);

                    for (int j = 1; j < ModelFacility.AssetTypes.AssetType[i].Assets.Asset.Count; j++)
                    {
                        ModelFacility.AssetTypes.AssetType[i].Assets.Asset[j] = null;
                    }
                }
            }
            
        }

        private void AddComment(object sender, RoutedEventArgs e)
        {
            Hyperlink h = sender as Hyperlink;
            if (h != null)
            {
                ReportResult res = null;
                string title = "";
                if (h.ToolTip.ToString() == "Create ER comment" && LstSpaceResults.SelectedItem != null)
                {
                    res = LstSpaceResults.SelectedItem as ReportResult;
                    title = lstSpaces.SelectedItem.ToString() + " ";
                    title += (res.BoolResult) ? "validation passed" : "validation failed";
                }
                else if (h.ToolTip.ToString() == "Create Class comment" && lstClassResults.SelectedItem != null)
                {                    
                    res = lstClassResults.SelectedItem as ReportResult;   
                    title = (res.BoolResult) ? "Validation passed" : "Validation failed";
                }
                if (res != null)
                {
                    Dictionary<string, object> MessageData = new Dictionary<string, object>();
                    string VerbalStatus = (res.BoolResult) ? "Information" : "Error";
                    string commentText =
                        string.Format("Entity {0} ({1}) {2} request {3}",
                            "Element " + res.EntityLabel,
                            res.EntityLabel,
                            (res.BoolResult) ? "passed" : "did not pass",
                            res.ConceptName
                            );

                    MessageData.Add("InstanceTitle", title);
                    MessageData.Add("CommentVerbalStatus", VerbalStatus);
                    MessageData.Add("CommentAuthor", "CB");
                    MessageData.Add("CommentText", commentText);
                    // MessageData.Add("DestinationEmail", res.Requirement.Author);
                    xpWindow.BroadCastMessage(this, "BcfAddInstance", MessageData);
                }
            }
        }

        bool UseAmber = true;

        private void TranspToggle(object sender, MouseButtonEventArgs e)
        {
            if (UseAmber)
                UnMatched.Fill = Brushes.Transparent;
            else
                UnMatched.Fill = Brushes.Orange;

            UseAmber = !UseAmber;
        }

        private void LstClassResults_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            xpWindow.DrawingControl.ZoomSelected();
        }
    }
}
