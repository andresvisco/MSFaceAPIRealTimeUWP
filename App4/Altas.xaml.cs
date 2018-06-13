using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Microsoft.ProjectOxford.Face.Contract;
using Microsoft.ProjectOxford.Face;
using App5;
using App4;
using Windows.Storage;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace App5
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Altas : Page
    {
        public static string subscriptionKey = "c568304102b84df291d2556d34c8d623";
        public static string subscriptionEndpoint = "https://eastus2.api.cognitive.microsoft.com/face/v1.0";
        public FaceServiceClient faceServiceClient = new FaceServiceClient(subscriptionKey, subscriptionEndpoint);
        public static string localValue = "1";
        Guid PersonID;
        


        public Altas()
        {
            ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            localValue = localSettings.Values["valorIdGroup"] as string;
            this.InitializeComponent();
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            
            



            var PersonaCreada =  await faceServiceClient.CreatePersonInLargePersonGroupAsync(localValue, personaNombre.Text.ToString());

            var ignored = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, () =>
            {
                if(PersonaCreada.PersonId.ToString() != "")
                {
                    PersonID = PersonaCreada.PersonId;
                    statusCreacion.Text = "Creado / " + PersonaCreada.PersonId.ToString();
                }
                


                //this.imagenCompletar.Source = bitmpatSRC;
                //bitmpatSRC.SetBitmapAsync(previewFrameBMO);
            });

            var valor = PersonaCreada.ToString();
        }

        private async void ButtonAgregarCara_Click(object sender, RoutedEventArgs e)
        {
           var subirImagen = await faceServiceClient.AddPersonFaceInLargePersonGroupAsync(localValue, PersonID, urlImagen.Text.ToString());
            var ignored = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, () =>
            {
                if (subirImagen.PersistedFaceId.ToString() != "")
                {

                    urlImagenStatus.Text = "Creado / "+ subirImagen.PersistedFaceId.ToString();
                }



                //this.imagenCompletar.Source = bitmpatSRC;
                //bitmpatSRC.SetBitmapAsync(previewFrameBMO);
            });
        }

        private async void ButtonEntrenar_Click(object sender, RoutedEventArgs e)
        {
           await faceServiceClient.TrainLargePersonGroupAsync(localValue);
        }
        public ObservableCollection<Tuple<string, string>> listItems = new ObservableCollection<Tuple<string, string>>();
        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            var response = await faceServiceClient.ListPersonsInLargePersonGroupAsync(localValue);
            


            foreach(var persona in response)
            {
                listItems.Add(new Tuple<string, string>(persona.PersonId.ToString(), persona.Name.ToString()));
            }
            

            itemListView.ItemsSource = listItems;

        }

        private void itemListView_ItemClick(object sender, ItemClickEventArgs e)
        {

            var idUsuario = ((System.Tuple<string, string>)e.ClickedItem).Item1;
            
            statusItem.Text = idUsuario.ToString();
        }

        private async void EliminarUsuario(object sender, RoutedEventArgs e)
        {
            Guid usuarioEliminar = new Guid(this.statusItem.Text.ToString());
            var resultado = await Eliminar(usuarioEliminar);
            if(resultado == "OK")
            {
                statusItemEliminado.Text = "Eliminado";

            }
            
            



        }
        async Task<string> Eliminar(Guid usuario)
        {
            
            await faceServiceClient.DeletePersonFromLargePersonGroupAsync(localValue, usuario);

            return("OK");
        }
       
    }
}
