﻿using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net;
using SpendLess.Client.Pages;
using System.Text.Json;
using SpendLess.Shared;


namespace SpendLess.Client.Services
{
	public class GoalService : IGoalService
	{

		public List<SpendLess.Shared.Goal> Goals { get; set; } = new List<SpendLess.Shared.Goal>();


		public GoalService(IHttpClientFactory clientFactory, ILocalStorageService localStorage, AuthenticationStateProvider authStateProvider)
		{
			_clientFactory = clientFactory;
			_localStorage = localStorage;
			_authStateProvider = authStateProvider;
		}

		private readonly IHttpClientFactory _clientFactory;
		private readonly AuthenticationStateProvider _authStateProvider;
		private readonly ILocalStorageService _localStorage;

		public async Task GetGoals()
		{
			var client = _clientFactory.CreateClient();

			var requestMessage = new HttpRequestMessage(HttpMethod.Get, "https://localhost:7290/api/Goals/GetGoals");
			string token = await _localStorage.GetItemAsStringAsync("token");
			requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", token.Replace("\"", ""));

			var response = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);
			if ((response.StatusCode) == HttpStatusCode.Unauthorized)
			{
				await _authStateProvider.GetAuthenticationStateAsync();
				//SnackBarService.ErrorMsg("Session has ended");
				return;
			}
			if (response.IsSuccessStatusCode)
			{
				var result = await response.Content.ReadFromJsonAsync<List<Goal>>();
				Goals = result;
			}


		
		}

		public async Task AddGoal(int userId, string name, double? amount, DateTime endDate, double? currentAmount)
		{
			var _httpClient = _clientFactory.CreateClient();
			try
			{
				string token = await _localStorage.GetItemAsStringAsync("token");
				_httpClient.DefaultRequestHeaders.Authorization =
						new AuthenticationHeaderValue("Bearer", token.Replace("\"", ""));


				var goal = new Goal(0, userId, name, amount, endDate, currentAmount);
				var response = await _httpClient.PostAsJsonAsync("https://localhost:7290/api/Goals/AddGoal", goal);
				if (response.IsSuccessStatusCode)
				{
					var id = await response.Content.ReadFromJsonAsync<int>();
					goal.Id = id;
					Goals.Add(goal);
					//SnackBarService.SuccessMsg("Succsesfully saved data");
				}
				if (response.StatusCode == HttpStatusCode.TooManyRequests)
				{

					//SnackBarService.ErrorMsg("Slow down");
					return;
				}
				else
				{
					//SnackBarService.ErrorMsg("Failed to save data!");
				}
			}
			catch (NullReferenceException ex)
			{
				await _httpClient.PostAsJsonAsync("https://localhost:7290/api/Exception", ex);
				throw;
			}
			catch (InvalidOperationException ex)
			{
				await _httpClient.PostAsJsonAsync("https://localhost:7290/api/Exception", ex);
				throw;
			}
			catch (JsonException ex)
			{
				await _httpClient.PostAsJsonAsync("https://localhost:7290/api/Exception", ex);
				throw;
			}
			catch (Exception ex)
			{
				await _httpClient.PostAsJsonAsync("https://localhost:7290/api/Exception", ex);
				throw;
			}
		}

        public async Task<string> ChangeCurrentAmount(Goal goal)
        {
			try
			{
				string token = await _localStorage.GetItemAsStringAsync("token");
				var _httpClient = _clientFactory.CreateClient();
				_httpClient.DefaultRequestHeaders.Authorization =
						new AuthenticationHeaderValue("Bearer", token.Replace("\"", ""));

				var response = await _httpClient.PutAsJsonAsync($"https://localhost:7290/api/Goals/PutGoal", goal);
				if (response.IsSuccessStatusCode)
				{
					//SnackBarService.SuccessMsg("Transaction was successfully deleted");

					//int c = 0;
					//foreach (var element in Goals)
					//{
					//    if (element.Id.Equals(goal.id))
					//    {
					//        Transactions.RemoveAt(c);
					//        break;
					//    }

					//    c++;
					//}
					//return "Transaction was successfully deleted";
				}
				if (response.StatusCode == HttpStatusCode.TooManyRequests)
				{

					//SnackBarService.ErrorMsg("Slow down");
					return "Failed to add value";
				}
				else
				{
					//SnackBarService.WarningMsg("Failed to delete transaction");
					return "Failed to add value";
				}
			}
			catch (NullReferenceException ex)
			{
				throw;
			}
			catch (InvalidOperationException ex)
			{
				throw;
			}
			catch (JsonException ex)
			{
				throw;
			}
			catch (Exception ex)
			{
				throw;
			}
		}
    }
}
