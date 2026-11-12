var LibraryQosWebTransport = {
	$qosWebTransportState: {
		instances: {},
		lastId: 0,
		onOpen: null,
		onDatagram: null,
		onError: null,

		emitError: function(id, msg) {
			if (!qosWebTransportState.onError || !qosWebTransportState.instances[id]) return;
			var msgBytes = lengthBytesUTF8(msg);
			var msgBuffer = _malloc(msgBytes + 1);
			stringToUTF8(msg, msgBuffer, msgBytes + 1);
			try {
				{{{ makeDynCall('vii', 'qosWebTransportState.onError') }}}(id, msgBuffer);
			} finally {
				_free(msgBuffer);
			}
		}
	},

	QosWebTransportSetOnOpen: function(callback) {
		qosWebTransportState.onOpen = callback;
	},

	QosWebTransportSetOnDatagram: function(callback) {
		qosWebTransportState.onDatagram = callback;
	},

	QosWebTransportSetOnError: function(callback) {
		qosWebTransportState.onError = callback;
	},

	QosWebTransportIsSupported: function() {
		return (typeof WebTransport !== 'undefined') ? 1 : 0;
	},

	/**
	 * Allocate an instance for the given URL without connecting, so the
	 * managed side can register it before any callback can fire.
	 *
	 * @returns instance ID
	 */
	QosWebTransportAllocate: function(url) {
		var id = qosWebTransportState.lastId++;
		qosWebTransportState.instances[id] = { url: UTF8ToString(url), wt: null, writer: null };
		return id;
	},

	QosWebTransportConnect: function(id) {
		var instance = qosWebTransportState.instances[id];
		if (!instance) return -1;

		if (typeof WebTransport === 'undefined') {
			qosWebTransportState.emitError(id, 'WebTransport is not supported in this browser.');
			return -2;
		}

		try {
			instance.wt = new WebTransport(instance.url);
		} catch (err) {
			qosWebTransportState.emitError(id, 'WebTransport constructor failed: ' + err);
			return -3;
		}

		instance.wt.closed.catch(function(err) {
			qosWebTransportState.emitError(id, 'WebTransport session closed: ' + err);
		});

		instance.wt.ready.then(function() {
			var datagrams = instance.wt.datagrams;
			// Chromium exposes datagrams.writable, WebKit only the newer createWritable().
			if (!datagrams || !datagrams.readable || (!datagrams.writable && typeof datagrams.createWritable !== 'function')) {
				qosWebTransportState.emitError(id, 'WebTransport datagrams are not supported on this session.');
				return null;
			}
			return Promise.resolve(datagrams.writable || datagrams.createWritable()).then(function(writable) {
				instance.writer = writable.getWriter();
				return datagrams;
			});
		}).then(function(datagrams) {
			if (!datagrams) return;

			var reader = datagrams.readable.getReader();
			var readLoop = function() {
				reader.read().then(function(result) {
					if (result.done) {
							qosWebTransportState.emitError(id, 'WebTransport session closed by the remote peer.');
							return;
						}
					if (qosWebTransportState.onDatagram) {
						var dataBuffer = result.value;
						var buffer = _malloc(dataBuffer.length);
						HEAPU8.set(dataBuffer, buffer);
						try {
							{{{ makeDynCall('viii', 'qosWebTransportState.onDatagram') }}}(id, buffer, dataBuffer.length);
						} finally {
							_free(buffer);
						}
					}
					readLoop();
				}).catch(function(err) {
					qosWebTransportState.emitError(id, 'WebTransport datagram read failed: ' + err);
				});
			};
			readLoop();

			if (qosWebTransportState.onOpen)
				{{{ makeDynCall('vi', 'qosWebTransportState.onOpen') }}}(id);
		}).catch(function(err) {
			qosWebTransportState.emitError(id, 'WebTransport connection failed: ' + err);
		});

		return 0;
	},

	QosWebTransportSend: function(id, bufferPtr, length) {
		var instance = qosWebTransportState.instances[id];
		if (!instance) return -1;
		if (!instance.writer) return -2;

		var buffer = new Uint8Array(length);
		buffer.set(HEAPU8.subarray(bufferPtr, bufferPtr + length));
		instance.writer.write(buffer).catch(function(err) {
			qosWebTransportState.emitError(id, 'WebTransport datagram write failed: ' + err);
		});
		return 0;
	},

	QosWebTransportClose: function(id) {
		var instance = qosWebTransportState.instances[id];
		if (!instance) return -1;

		try {
			if (instance.wt)
				instance.wt.close();
		} catch (err) {}

		delete qosWebTransportState.instances[id];
		return 0;
	}
};

autoAddDeps(LibraryQosWebTransport, '$qosWebTransportState');
mergeInto(LibraryManager.library, LibraryQosWebTransport);
