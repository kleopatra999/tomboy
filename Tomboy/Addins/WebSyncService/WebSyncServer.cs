// Permission is hereby granted, free of charge, to any person obtaining 
// a copy of this software and associated documentation files (the 
// "Software"), to deal in the Software without restriction, including 
// without limitation the rights to use, copy, modify, merge, publish, 
// distribute, sublicense, and/or sell copies of the Software, and to 
// permit persons to whom the Software is furnished to do so, subject to 
// the following conditions: 
//  
// The above copyright notice and this permission notice shall be 
// included in all copies or substantial portions of the Software. 
//  
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, 
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF 
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND 
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE 
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION 
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION 
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE. 
// 
// Copyright (c) 2008 Novell, Inc. (http://www.novell.com) 
// 
// Authors: 
//      Sandy Armstrong <sanfordarmstrong@gmail.com>
// 

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Tomboy.Sync;
using Tomboy.WebSync.Api;

namespace Tomboy.WebSync
{
	public class WebSyncServer : SyncServer
	{
		private string serverUrl;
		private string userName;
		private UserInfo user;
		private List<NoteInfo> pendingCommits;
		
		public WebSyncServer (string serverUrl, string userName)
		{
			this.serverUrl = serverUrl;
			this.userName = userName;

			pendingCommits = new List<NoteInfo> ();
		}

		#region SyncServer implementation
		
		public bool BeginSyncTransaction ()
		{
			// TODO: Check connection and auth
			RefreshUser ();
			return true;
		}
		
		public bool CancelSyncTransaction ()
		{
			// TODO: Cancel any pending request
			return true;
		}
		
		public bool CommitSyncTransaction ()
		{
			RefreshUser ();	// TODO: Test that latest sync rev hasn't changed
			user.UpdateNotes (pendingCommits);
			// TODO: Check for problems
			return true;
		}
		
		public SyncLockInfo CurrentSyncLock {
			get {
				return null;
			}
		}
		
		public void DeleteNotes (IList<string> deletedNoteUUIDs)
		{
			foreach (string uuid in deletedNoteUUIDs) {
				NoteInfo noteInfo = new NoteInfo ();
				noteInfo.Command = "delete";
				noteInfo.Guid = uuid;
				pendingCommits.Add (noteInfo);
			}
		}
		
		public IList<string> GetAllNoteUUIDs ()
		{
			throw new System.NotImplementedException();
		}
		
		public IDictionary<string, NoteUpdate> GetNoteUpdatesSince (int revision)
		{
			RefreshUser ();	// TODO: Test that latest sync rev hasn't changed
			Dictionary<string, NoteUpdate> updates =
				new Dictionary<string, NoteUpdate> ();
			foreach (NoteInfo noteInfo in user.GetNotes (true, revision)) {
				string noteXml = CreateNoteXml (noteInfo);
				NoteUpdate update = new NoteUpdate (noteXml,
				                                    noteInfo.Title,
				                                    noteInfo.Guid,
				                                    noteInfo.LastSyncRevision);
				updates.Add (noteInfo.Guid, update);
			}
			return updates;
		}
		
		public string Id {
			get {
				return serverUrl;
			}
		}
		
		public int LatestRevision {
			get {
				RefreshUser ();	// TODO: Test that latest sync rev hasn't changed
				return user.LatestSyncRevision;
			}
		}
		
		public void UploadNotes (IList<Note> notes)
		{
			foreach (Note note in notes) {
				pendingCommits.Add (CreateNoteInfo (note));
			}
		}
		
		#endregion

		#region Private Methods
		
		private void RefreshUser ()
		{
			user = UserInfo.GetUser (serverUrl + "/api/1.0/" + userName);
		}

		private NoteInfo CreateNoteInfo (Note note)
		{
			NoteInfo noteInfo = new NoteInfo ();
			
			noteInfo.Guid = note.Id;
			noteInfo.Title = note.Title;
			noteInfo.OpenOnStartup = note.IsOpenOnStartup;
			noteInfo.CreateDate = note.CreateDate;
			noteInfo.LastChangeDate = note.ChangeDate;
			noteInfo.LastMetadataChangeDate = note.MetadataChangeDate;

			noteInfo.Tags = new List<string> ();
			foreach (Tag tag in note.Tags)
				noteInfo.Tags.Add (tag.Name);

			// TODO: content
			const string noteContentRegex =
				@"\A<note-content(\s+version=""(?<contentVersion>[^""]+)"")?>(?<innerContent>.*)</note-content>\Z";
			Match m = Regex.Match (note.XmlContent, noteContentRegex);
			Group versionGroup = m.Groups ["contentVersion"];
			Group contentGroup = m.Groups ["innerContent"];

			double contentVersion;
			if (versionGroup.Success &&
			    double.TryParse (versionGroup.Value, out contentVersion)) {
				noteInfo.NoteContentVersion = contentVersion;
			} else
				noteInfo.NoteContentVersion = 0.1;

			if (contentGroup.Success)
				noteInfo.NoteContent = contentGroup.Value;
			else
				noteInfo.NoteContent = string.Empty;

			return noteInfo;
		}

		private string CreateNoteXml (NoteInfo noteInfo)
		{
			NoteData noteData = new NoteData (noteInfo.Guid);
			noteData.Title = noteInfo.Title;
			noteData.Text =
				"<note-content version=\"" + noteInfo.NoteContentVersion.ToString () + "\">" +
				noteInfo.NoteContent + "</note-content>";
			noteData.ChangeDate = noteInfo.LastChangeDate;
			noteData.MetadataChangeDate = noteInfo.LastMetadataChangeDate;
			noteData.CreateDate = noteInfo.CreateDate;
			noteData.IsOpenOnStartup = noteInfo.OpenOnStartup;

			foreach (string tagName in noteInfo.Tags) {
				Tag tag = TagManager.GetOrCreateTag (tagName);
				noteData.Tags [tag.NormalizedName] = tag;
			}
			
			return NoteArchiver.WriteString (noteData);
		}

		#endregion
	}
}