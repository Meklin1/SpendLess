﻿using Microsoft.IdentityModel.Tokens;
using MudBlazor;
using SpendLess.Shared;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Xml.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SpendLess.Client.Services
{

    public class TransactionService : ITransactionService
    {

        public event EventHandler<EventArgs>? TransactionsChanged;
        public async Task OnTransactionsChanged()
        {
            if (TransactionsChanged is not null)
                TransactionsChanged.Invoke(this, EventArgs.Empty);
        }

        public TransactionService(IHttpClientFactory clientFactory, ILocalStorageService localStorage, AuthenticationStateProvider authStateProvider, ISnackBarService snackBarService)
        {
            _clientFactory = clientFactory;
            _localStorage = localStorage;
            _authStateProvider = authStateProvider;
            _snackBarService= snackBarService;
        }

        private readonly ISnackBarService _snackBarService;
        private readonly IHttpClientFactory _clientFactory;
        private readonly AuthenticationStateProvider _authStateProvider;
        private readonly ILocalStorageService _localStorage;
        public List<SpendLess.Shared.Transactions> Transactions { get; set; } = new List<SpendLess.Shared.Transactions>();
        public string UserName { get; set; } = "Name not found";

        public delegate void LogException(HttpClient client, string str, Exception ex);



        public async Task<bool> Savelist(double? amount, bool toggleExpenseIncome, string? textValue, string? categoryValue, DateTime? date, bool togglePeriodical, int interval, string period, DateTime? endDate)
        {
            if (amount < 0){
                //SnackBarService.WarningMsg("Amount can not be negative or zero!");
                return false;
            }
            if (toggleExpenseIncome == true)
            {
                categoryValue = "Income";
            }
            if (textValue == null){
                textValue = "Transaction";
            }
            if (categoryValue != null && date.HasValue && amount != null){
                if (toggleExpenseIncome == false){
                    amount = -amount;
                }

                if (togglePeriodical == true)
                {
                    await AddPeriodicTransaction(amount, categoryValue, date ?? DateTime.MinValue, textValue, period, interval, endDate);
                }
                else
                {
                    await AddTransaction(amount, categoryValue, date ?? DateTime.MinValue, textValue);
                }
                Transactions.Sort();
                await Task.Delay(1);

                categoryValue = null;
                amount = null;
                return true;
            }

            else{
                //SnackBarService.WarningMsg("Fields can not be empty!");
                return false;
            }
            textValue = null;
            await OnTransactionsChanged();
        }




        public async Task GetTransactions()
        {           
            var client = _clientFactory.CreateClient();
            try
            {
                var requestMessage = new HttpRequestMessage(HttpMethod.Get, "https://localhost:7290/api/Transactions/GetTransactions");                
                string token = await _localStorage.GetItemAsStringAsync("token");
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", token.Replace("\"", ""));

                var response = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);
                if ((response.StatusCode) == HttpStatusCode.Unauthorized){
                    await _authStateProvider.GetAuthenticationStateAsync();
                    _snackBarService.ErrorMsg("Session has ended");
                    return;
                }
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<List<Transactions>>();
                    Transactions = result;
                }              
            }
            //        catch (NullReferenceException ex)
            //        {
            //logException(client, "https://localhost:7290/api/Exception", ex);
            //throw;
            //        }
            //        catch (InvalidOperationException ex)
            //        {
            //            logException(client, "https://localhost:7290/api/Exception", ex);
            //            throw;
            //        }
            //        catch (JsonException ex)
            //        {
            //            logException(client, "https://localhost:7290/api/Exception", ex);
            //            throw;
            //        }
            catch (Exception ex){
                await client.PostAsJsonAsync("https://localhost:7290/api/Exception", ex);
                throw;
            }


            /*var _httpClient = clientFactory.CreateClient();
            var result = await _httpClient.GetFromJsonAsync<List<Transaction>>("api/finance/GetTransactions");
            if(result != null)
            {
                Transactions = result;
                SnackBarService.SuccessMsg("Data loaded");
            }*/
            await this.OnTransactionsChanged();
        }

        public async Task AddTransaction(double? amount, string category, DateTime date, string comment)
        {
            var _httpClient = _clientFactory.CreateClient();
            try
            {
                string token = await _localStorage.GetItemAsStringAsync("token");                
                _httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", token.Replace("\"", ""));


                var transaction = new Transactions(null, amount, category, date, comment);
                var response = await _httpClient.PostAsJsonAsync("https://localhost:7290/api/Transactions/AddTransaction", transaction);             
                if (response.IsSuccessStatusCode)
                {
                    var id = await response.Content.ReadFromJsonAsync<int>();
                    transaction.Id = id;
                    Transactions.Add(transaction);

                    _snackBarService.SuccessMsg("Succsesfully saved data");
                }
                else if (response.StatusCode == HttpStatusCode.TooManyRequests){
                    _snackBarService.ErrorMsg("Slow down");
                    return;
                }
                else{
                    _snackBarService.ErrorMsg("Failed to save data!");
                }
            }
            //catch(NullReferenceException ex)
            //{
            //    await _httpClient.PostAsJsonAsync("https://localhost:7290/api/Exception", ex);
            //    throw;
            //}
            //catch (InvalidOperationException ex)
            //{
            //    await _httpClient.PostAsJsonAsync("https://localhost:7290/api/Exception", ex);
            //    throw;
            //}
            //catch(JsonException ex)
            //{
            //    await _httpClient.PostAsJsonAsync("https://localhost:7290/api/Exception", ex);
            //    throw;
            //}
            catch(Exception ex){
                await _httpClient.PostAsJsonAsync("https://localhost:7290/api/Exception", ex);
                throw;
            }
        }

        public async Task AddPeriodicTransaction(double? amount, string category, DateTime date, string comment, string period, int interval, DateTime? endDate)
        {
            var _httpClient = _clientFactory.CreateClient();
            try
            {
                string token = await _localStorage.GetItemAsStringAsync("token");               
                _httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", token.Replace("\"", ""));
                if (endDate == null){
                    endDate = date.AddMonths(12);
                }
                bool isMonthly = false;
                switch (period) {
                    case "day(s)":
                        break;
                    case "week(s)":
                        interval = interval * 7;
                        break;
                    case "month(s)":
                        isMonthly = true;
                        break;
                    default: throw new ArgumentException("Period is incorrect"); //add exception for logging here?
                }

                List<Transactions> transactions = new List<Transactions>();

                while (date <= endDate)
                {
                    transactions.Add(new Transactions(null, amount, category, date, comment, null, period, interval, endDate));

                    if (isMonthly){
                        date = date.AddMonths(interval);
                    }
                    else{
                        date = date.AddDays(interval);
                    }
                }

                var response = await _httpClient.PostAsJsonAsync("https://localhost:7290/api/Transactions/AddPeriodicTransaction", transactions);
                var transactionsID = await response.Content.ReadFromJsonAsync<List<Transactions?>>();

                if (response.IsSuccessStatusCode)
                {
                    Transactions.AddRange(transactionsID);
                    _snackBarService.SuccessMsg("Succsesfully saved data");
                }
                else{
                    _snackBarService.ErrorMsg("Failed to save data!");
                }
            }
            //catch (InvalidOperationException ex)
            //{
            //    await _httpClient.PostAsJsonAsync("https://localhost:7290/api/Exception", ex);
            //    throw;
            //}
            //catch (JsonException ex)
            //{
            //    await _httpClient.PostAsJsonAsync("https://localhost:7290/api/Exception", ex);
            //    throw;
            //}
            //catch(ArgumentException ex)
            //{
            //    await _httpClient.PostAsJsonAsync("https://localhost:7290/api/Exception", ex);
            //    throw;
            //}
            catch (Exception ex){
                await _httpClient.PostAsJsonAsync("https://localhost:7290/api/Exception", ex);
                throw;
            }
        }

        public async Task<string> DeleteTransaction(int id)
        {
            try
            {
                string token = await _localStorage.GetItemAsStringAsync("token");
                var _httpClient = _clientFactory.CreateClient();
                _httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", token.Replace("\"", ""));

                var response = await _httpClient.DeleteAsync($"https://localhost:7290/api/Transactions/{id}");
                if (response.IsSuccessStatusCode)
                {
                    _snackBarService.SuccessMsg("Transaction was successfully deleted");
                    
                    int c = 0;
                    foreach (var element in Transactions)
                    {
                        if (element.Id.Equals(id)){
                            Transactions.RemoveAt(c);
                            break;
                        }
                        c++;
                    }
                    return "Transaction was successfully deleted";
                }
                if (response.StatusCode == HttpStatusCode.TooManyRequests){

                    _snackBarService.ErrorMsg("Slow down");
                    return "Failed to delete transaction";
                }
                else{
                    _snackBarService.WarningMsg("Failed to delete transaction");
                    return "Failed to delete transaction";
                }
            }
            //catch (NullReferenceException ex)
            //{
            //    throw;
            //}
            //catch (InvalidOperationException ex)
            //{
            //    throw;
            //}
            //catch (JsonException ex)
            //{
            //    throw;
            //}
            catch (Exception ex){
                throw;
            }
        }

        

        public async Task GetUserName()
        {
            var client = _clientFactory.CreateClient();
            try
            {
                var requestMessage = new HttpRequestMessage(HttpMethod.Get, "https://localhost:7290/api/Transactions/GetUser");
                string token = await _localStorage.GetItemAsStringAsync("token");
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", token.Replace("\"", ""));

                var response = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);
                if ((response.StatusCode) == HttpStatusCode.Unauthorized)
                {
                    await _authStateProvider.GetAuthenticationStateAsync();
                    _snackBarService.ErrorMsg("Session has ended");
                    UserName = "Name not found";
                }

                var result = await response.Content.ReadFromJsonAsync<Server.Models.User>();
                UserName = result.Name;
                
            }
            catch (Exception ex)
            {
                await client.PostAsJsonAsync("https://localhost:7290/api/Exception", ex);
                throw;
            }
        }

    }
}
