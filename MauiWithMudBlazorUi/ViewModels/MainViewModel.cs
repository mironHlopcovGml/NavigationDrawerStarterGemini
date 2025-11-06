
using EfcToXamarinAndroid.Core.Configs.ManagerCore;
using EfcToXamarinAndroid.Core.Repository;
using EfcToXamarinAndroid.Core.Services;
using EfcToXamarinAndroid.MigrationsHelper.Migrations;
using MauiAppWithMudBlazor.Components.Models;

using PermissionStatus = EfcToXamarinAndroid.Core.Services.PermissionStatus;

namespace EfcToXamarinAndroid.Core.ViewModels
{
    public class MainViewModel
    {
        private readonly ISmsReader _smsReader;
        private readonly IFileService _fileService;
        private readonly IDataService _dataService;
        private readonly IUIService _uiService;
        private readonly IPermissionService _permissionService;
        private readonly AppConfiguration _appConfiguration;

        public List<FinanceItem> AllItems { get; private set; } = [];
        public Dictionary<OperacionTyps, List<FinanceItem>> FilteredItems { get; private set; } = new();

        public event EventHandler? DataUpdated;

        public MainViewModel(
            ISmsReader smsReader,
            IFileService fileService,
            IDataService dataService,
            IUIService uiService,
            IPermissionService permissionService)
        {
            _smsReader = smsReader;
            _fileService = fileService;
            _dataService = dataService;
            _uiService = uiService;
            _permissionService = permissionService;
            var configManager = ConfigurationManager.ConfigManager;
            _appConfiguration = configManager.BankConfigurationFromJson;
        }

        public async Task InitializeAsync()
        {
            _smsReader.SmsReceived += _smsReader_SmsReceived;
            // Инициализация БД и загрузка данных на фоне
            await DatesRepositorio.SetDatasFromDB();  // EF Core
            await LoadFinanceItemsAsync();            // Преобразование DataItem -> FinanceItem
            DatesRepositorio.PaymentsChanged += (_, __) => RefreshData();
            DatesRepositorio.DepositsChanged += (_, __) => RefreshData();
            DatesRepositorio.CashsChanged += (_, __) => RefreshData();
            DatesRepositorio.UnreachableChanged += (_, __) => RefreshData();

        }
        private async void RefreshData() => await LoadFinanceItemsAsync();
        public async Task LoadFinanceItemsAsync()
        {
            var data = DatesRepositorio.DataItems ?? [];

            AllItems = data
                .Select(item => new FinanceItem
                {
                    Id = item.Id,
                    Date = item.Date,
                    Sum = item.Sum,
                    Description = item.Descripton,
                    Title = item.Title,
                    MccDescription = item.MccDeskription,
                    MCC = item.MCC,
                    UnreachableText = item.UnreachableText,
                    OperationType = item.OperacionTyp,
                    IsNewDataItem = item.IsNewDataItem,
                    Balance = item.Balance
                })
                .OrderByDescending(x => x.Date)
                .ToList();

            UpdateFilteredCache();
            DataUpdated?.Invoke(this, EventArgs.Empty);
            await Task.CompletedTask;
        }
        private void UpdateFilteredCache()
        {
            FilteredItems.Clear();
            foreach (OperacionTyps type in Enum.GetValues(typeof(OperacionTyps)))
            {
                FilteredItems[type] = AllItems
                    .Where(f => f.OperationType.ToString() == type.ToString())
                    .ToList();
            }
        }
        public IEnumerable<FinanceItem> GetFilteredItems(OperacionTyps type)
        => FilteredItems.TryGetValue(type, out var list) ? list : Enumerable.Empty<FinanceItem>();

        public IEnumerable<string?> GetDeskriptions(OperacionTyps type)
        {
            if (FilteredItems.TryGetValue(type, out var deskrList))
            {
                var deskrps = deskrList.Select(x => x.Description).Distinct();
                if (deskrps != null)
                    return deskrps;
            }

            return Enumerable.Empty<string>();

        }

        private async void _smsReader_SmsReceived(object? sender, Sms sms)
        {
            var smsList = new List<Sms>() { sms };
            var dataItems = await _dataService.ParseSmsToDataItemsAsync(smsList, _appConfiguration.Banks);
            if (dataItems?.Count() > 0)
            {
                await DatesRepositorio.AddDatas(dataItems);
            }
        }

        public async Task ProcessSmsDataAsync()
        {

            var smsList = await _smsReader.GetAllSmsAsync(_appConfiguration.Banks);
            var dataItems = await _dataService.ParseSmsToDataItemsAsync(smsList, _appConfiguration.Banks);

            if (dataItems?.Count() > 0)
            {
                await DatesRepositorio.AddDatas(dataItems);
            }
        }

        public async Task ProcessPdfFileAsync(string filePath)
        {
            try
            {
                var configManager = ConfigurationManager.ConfigManager;
                var configuration = configManager.BankConfigurationFromJson;

                var dataItems = await _dataService.ParsePdfToDataItemsAsync(filePath, configuration.Banks);
                await DatesRepositorio.AddDatas(dataItems);

                await _uiService.ShowToastAsync("Файл обработан.");
            }
            catch (Exception ex)
            {
                await _uiService.ShowToastAsync("Произошла ошибка при обработке файла.");
            }
        }

        public async Task ExportDataAsync()
        {
            try
            {
                var downloadsPath = _fileService.GetDownloadsPath(); //await _fileService.PickFolderAsync();//
                var fileName = $"FinReport{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.xml";
                var filePath = System.IO.Path.Combine(downloadsPath, fileName);

                var success = await _dataService.SaveDataItemsToXmlAsync(filePath);

                if (success)
                {
                    await _uiService.ShowToastAsync("Данные экспортированы.");
                }
                else
                {
                    await _uiService.ShowToastAsync("Ошибка экспорта данных.");
                }
            }
            catch (Exception ex)
            {
                await _uiService.ShowToastAsync("Ошибка экспорта данных.");
            }
        }

        public async Task ImportDataAsync()
        {
            try
            {
                var filePath = await _fileService.PickFileAsync("Выберите файл для импорта", [".xml"]);
                if (!string.IsNullOrEmpty(filePath))
                {
                    var dataItems = await _dataService.ParseXmlToDataItemsAsync(filePath);
                    await DatesRepositorio.AddDatas(dataItems.ToList());
                    await _uiService.ShowToastAsync("Данные импортированы.");
                }
            }
            catch (Exception ex)
            {
                await _uiService.ShowToastAsync("Ошибка импорта данных.");
            }
        }

        public async Task ClearDatabaseAsync()
        {
            var confirmed = await _uiService.ShowConfirmationDialogAsync(
                "Очистка базы данных",
                "Данное действие приведет к полной очистке базы данных приложения. Вы действительно хотите продолжить?");

            if (confirmed)
            {
                var success = await DatesRepositorio.DeleteAllItems();
                var message = success ? "База данных очищена." : "Произошла ошибка очистки базы данных.";
                await _uiService.ShowToastAsync(message);
            }
        }
        public async Task UpdateItemValueAsync(int id, FinanceItem item)
        {
            DataItem dataItem = new DataItem();// await DatesRepositorio.GetDataItem(id);

            dataItem.Date = item.Date;
            dataItem.Sum = item.Sum;
            dataItem.Descripton = item.Description;
            dataItem.Title = item.Title;
            dataItem.MccDeskription = item.MccDescription;
            dataItem.MCC = item.MCC;
            dataItem.UnreachableText = item.UnreachableText;
            dataItem.OperacionTyp = item.OperationType;
            dataItem.IsNewDataItem = item.IsNewDataItem;
            dataItem.Balance = item.Balance;

            await DatesRepositorio.UpdateItemValue(id, dataItem);
        }
        public async Task<FinanceItem> GetFinItem(int id)
        {
            var item = await DatesRepositorio.GetDataItem(id);
            var finItem = new FinanceItem
            {
                Id = item.Id,
                Date = item.Date,
                Sum = item.Sum,
                Description = item.Descripton,
                Title = item.Title,
                MccDescription = item.MccDeskription,
                MCC = item.MCC,
                UnreachableText = item.UnreachableText,
                OperationType = item.OperacionTyp,
                IsNewDataItem = item.IsNewDataItem,
                Balance = item.Balance
            };
            return finItem;
        }

        public async Task ResetFinItem(FinanceItem item)
        {
            var oldItem = await DatesRepositorio.GetDataItem(item.Id);

            item.Id = oldItem.Id;
            item.Date = oldItem.Date;
            item.Sum = oldItem.Sum;
            item.Description = oldItem.Descripton;
            item.Title = oldItem.Title;
            item.MccDescription = oldItem.MccDeskription;
            item.MCC = oldItem.MCC;
            item.UnreachableText = oldItem.UnreachableText;
            item.OperationType = oldItem.OperacionTyp;
            item.IsNewDataItem = oldItem.IsNewDataItem;
            item.Balance = oldItem.Balance;

        }

        public async Task<bool> CheckPermissionsAsync()
        {
            var smsPermission = await _permissionService.CheckSmsPermissionAsync();
            var storageReadPermission = await _permissionService.CheckStorageReadPermissionAsync();
            var storageWritePermission = await _permissionService.CheckStorageWritePermissionAsync();

            if (smsPermission != PermissionStatus.Granted)
            {
                await _permissionService.RequestSmsPermissionAsync();
            }

            if (storageReadPermission != PermissionStatus.Granted)
            {
                await _permissionService.RequestStorageReadPermissionAsync();
            }

            if (storageWritePermission != PermissionStatus.Granted)
            {
                await _permissionService.RequestStorageWritePermissionAsync();
            }

            return smsPermission == PermissionStatus.Granted &&
                   storageReadPermission == PermissionStatus.Granted &&
                   storageWritePermission == PermissionStatus.Granted;
        }

        public async Task<List<string>> GetTags()
        {
            return DatesRepositorio.GetTags();
        }
        public IEnumerable<int> GetMccCodes(OperacionTyps type)
        {
            if (FilteredItems.TryGetValue(type, out var mccList))
            {
                var mccs = mccList.Select(x => x.MCC).Distinct();
                if (mccs != null)
                    return mccs;
            }

            return Enumerable.Empty<int>();

        }
        public IEnumerable<string> GetMccDeskriptons(OperacionTyps type)
        {
            if (FilteredItems.TryGetValue(type, out var mccDiskrList))
            {
                var mccDs = mccDiskrList.Select(x => x.MccDescription).Distinct();
                if (mccDs != null)
                    return mccDs;
            }

            return Enumerable.Empty<string>();

        }
        // Вставьте следующий метод сюда, например, после метода AddItem()      
        public async Task<IEnumerable<FinanceItem>> GetFinanceItemsChunk(GetItemsRequest request)
        {
           

            var items =  await DatesRepositorio.GetDataItems(request);
            var  fintems = items
              .Select(item => new FinanceItem
              {
                  Id = item.Id,
                  Date = item.Date,
                  Sum = item.Sum,
                  Description = item.Descripton,
                  Title = item.Title,
                  MccDescription = item.MccDeskription,
                  MCC = item.MCC,
                  UnreachableText = item.UnreachableText,
                  OperationType = item.OperacionTyp,
                  IsNewDataItem = item.IsNewDataItem,
                  Balance = item.Balance
              })
              .OrderByDescending(x => x.Date)
              .ToList();
            return fintems;
        }
    }
}