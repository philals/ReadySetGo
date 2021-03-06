﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using ReadySetGo.Library.DataContracts;

namespace ReadySetGo.Library
{
    public class SpotifyService : ISpotifyService
    {
        readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IRequestLogger _requestLogger;
        private readonly HttpClient _client;

        public SpotifyService(IHttpContextAccessor httpContextAccessor, IRequestLogger requestLogger)
        {
            _httpContextAccessor = httpContextAccessor;
            _requestLogger = requestLogger;

            _client = new HttpClient();

            _client.BaseAddress = new Uri("https://api.spotify.com/v1");
        }

        public string CreatePlaylist(TokenResponse token, PlaylistResult playlist)
        {
            _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token.AccessToken}");

            var user = GetUser();

            _requestLogger.LogSpotify(user, playlist, token);

            var createdPlaylist = CreateNew(user, playlist.ArtistName);

            var tracklist = new Tracklist{Uris = new List<String>()};

            foreach (var song in playlist.Songs)
            {
                try
                {
                    tracklist.Uris.Add(GetTrack(playlist.ArtistName, song.Name).Uri);
                }
                catch (Exception)
                {
                    try
                    {
                        foreach (var songName in song.Name.Split("/"))
                        {
                            tracklist.Uris.Add(GetTrack(playlist.ArtistName, songName.Trim()).Uri);
                        }
                    }
                    catch (Exception)
                    {

                    }
                }
            }

            SetTracks(user, createdPlaylist, tracklist);

            return createdPlaylist.ExternalUrls.Spotify;
        }

        private SpotifyUser GetUser()
        {
            var response = _client.GetAsync("/v1/me").Result;

            var user = JsonConvert.DeserializeObject<SpotifyUser>(response.Content.ReadAsStringAsync().Result);

            return user;
        }

        private SpotifyPlaylist CreateNew(SpotifyUser user, string artistName)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/users/{user.Id}/playlists")
            {
                Content = new StringContent($"{{\"name\":\"{artistName} - ReadySetGo\", \"public\":false}}",
                                                Encoding.UTF8,
                                                "application/json")
            };

            var response = _client.SendAsync(request).Result;

            var createdPlaylist = JsonConvert.DeserializeObject<SpotifyPlaylist>(response.Content.ReadAsStringAsync().Result);
                
            return createdPlaylist;
        }

        private Item GetTrack(string artistName, string songName)
        {
            var response = _client.GetAsync($"/v1/search?q={WebUtility.UrlEncode(songName)}%20artist:%22{WebUtility.UrlEncode(artistName)}%22&type=track&limit=1").Result;

            var searchResult = JsonConvert.DeserializeObject<SearchResult>(response.Content.ReadAsStringAsync().Result);

            return searchResult.Tracks.Items.Single();
        }

        private void SetTracks(SpotifyUser user, SpotifyPlaylist createdPlaylist, Tracklist tracklist)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, $"/v1/users/{user.Id}/playlists/{createdPlaylist.Id}/tracks")
            {
                Content = new StringContent(JsonConvert.SerializeObject(tracklist),
                                                Encoding.UTF8,
                                                "application/json")
            };

            var response = _client.SendAsync(request).Result;
        }
    }

    public static class SessionExtensions
    {
        public static void SetObjectAsJson(this ISession session, string key, object value)
        {
            session.SetString(key, JsonConvert.SerializeObject(value));
        }

        public static T GetObjectFromJson<T>(this ISession session, string key)
        {
            var value = session.GetString(key);

            return value == null ? default(T) : JsonConvert.DeserializeObject<T>(value);
        }
    }
}
