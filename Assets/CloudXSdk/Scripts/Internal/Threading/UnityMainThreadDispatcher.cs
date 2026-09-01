/*
Copyright 2015 Pim de Witte All Rights Reserved.

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using UnityEngine;
using System.Collections;
using System.Collections.Concurrent;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CloudX.Internal.Threading {
	/// Author: Pim de Witte (pimdewitte.com) and contributors, https://github.com/PimDeWitte/UnityMainThreadDispatcher
	/// <summary>
	/// A thread-safe class which holds a queue with actions to execute on the next Update() method. It can be used to make calls to the main thread for
	/// things such as UI Manipulation in Unity. It was developed for use in combination with the Firebase Unity plugin, which uses separate threads for event handling
	/// </summary>
	internal class UnityMainThreadDispatcher : MonoBehaviour {

		private static readonly ConcurrentQueue<Action> _executionQueue = new ConcurrentQueue<Action>();

		public void Update() {
			var actionsToProcess = _executionQueue.Count;
			for (var i = 0; i < actionsToProcess; i++) {
				if (!_executionQueue.TryDequeue(out var action)) {
					break;
				}

				try
				{
					action.Invoke();
				}
				catch (Exception ex)
				{
					Debug.LogException(ex);
				}
			}
		}

		/// <summary>
		/// Adds the IEnumerator to the queue
		/// </summary>
		/// <param name="action">IEnumerator function that will be executed from the main thread.</param>
		public void Enqueue(IEnumerator action) {
			_executionQueue.Enqueue (() => {
				StartCoroutine (action);
			});
		}

		/// <summary>
		/// Adds the Action to the queue
		/// </summary>
		/// <param name="action">function that will be executed from the main thread.</param>
		public void Enqueue(Action action)
		{
			_executionQueue.Enqueue(action);
		}

		/// <summary>
		/// Adds the Action to the queue, returning a Task which is completed when the action completes
		/// </summary>
		/// <param name="action">function that will be executed from the main thread.</param>
		/// <returns>A Task that can be awaited until the action completes</returns>
		public Task EnqueueAsync(Action action)
		{
			var tcs = new TaskCompletionSource<bool>();

			void WrappedAction() {
				try
				{
					action();
					tcs.TrySetResult(true);
				} catch (Exception ex)
				{
					tcs.TrySetException(ex);
				}
			}

			Enqueue(WrappedAction);
			return tcs.Task;
		}


		private static UnityMainThreadDispatcher _instance = null;

		/// <summary>
		/// Managed thread id of the Unity main thread, captured when the dispatcher is created
		/// (Awake runs on the main thread). Null until then.
		/// </summary>
		public static int? MainThreadId { get; private set; }

		public static bool Exists() {
			return _instance != null;
		}

		public static UnityMainThreadDispatcher Instance() {
			if (!Exists ()) {
				throw new Exception ("UnityMainThreadDispatcher could not find the UnityMainThreadDispatcher object. Please ensure the SDK is properly initialized.");
			}
			return _instance;
		}


		void Awake() {
			if (RegisterInstance()) {
				DontDestroyOnLoad(this.gameObject);
			}
		}

		/*
		 * Records the main thread id and claims the singleton slot. Split out of Awake so Edit Mode
		 * tests (where Awake is not invoked on AddComponent and DontDestroyOnLoad is illegal) can
		 * register an instance. Returns true when this component became the instance.
		 */
		internal bool RegisterInstance() {
			if (_instance != null) {
				return false;
			}
			_instance = this;
			MainThreadId = Thread.CurrentThread.ManagedThreadId;
			// A newly registered dispatcher re-arms CallbackDispatcher's missing-dispatcher error.
			CallbackDispatcher.ResetMissingDispatcherWarning();
			return true;
		}

		/// <summary>
		/// Enqueues the action if a dispatcher instance exists, reading the instance once so a
		/// concurrent OnDestroy cannot make this throw. Returns false when there is no instance.
		/// </summary>
		public static bool TryEnqueue(Action action) {
			var instance = _instance;
			if (instance == null) {
				return false;
			}
			instance.Enqueue(action);
			return true;
		}

		internal void OnDestroy() {
			// Only the registered instance may clear the slot; a stray second component must not.
			if (_instance == this) {
				_instance = null;
				/*
				 * Drop anything still queued: the queue is static, so with domain reload disabled
				 * it would survive into the next play session and run stale closures there.
				 */
				while (_executionQueue.TryDequeue(out _)) {
				}
			}
		}


	}
}
