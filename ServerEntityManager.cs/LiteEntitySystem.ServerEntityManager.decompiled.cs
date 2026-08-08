using System;
using System.Collections.Generic;
using K4os.Compression.LZ4;
using LiteEntitySystem.Collections;
using LiteEntitySystem.Internal;
using LiteEntitySystem.Transport;
using LiteNetLib;
using LiteNetLib.Utils;

namespace LiteEntitySystem;

/// <summary>
/// Server entity manager
/// </summary>
public sealed class ServerEntityManager : EntityManager
{
	public const int MaxStoredInputs = 30;

	private readonly IdGeneratorUShort _entityIdQueue = new IdGeneratorUShort(1, 64000);

	private readonly IdGeneratorByte _playerIdQueue = new IdGeneratorByte(1, 254);

	private readonly Queue<RemoteCallPacket> _rpcPool = new Queue<RemoteCallPacket>();

	private readonly Queue<byte[]> _pendingClientRequests = new Queue<byte[]>();

	private byte[] _packetBuffer = new byte[257 * NetConstants.MaxPacketSize + 32767];

	private readonly SparseMap<NetPlayer> _netPlayers = new SparseMap<NetPlayer>(255);

	private readonly StateSerializer[] _stateSerializers = new StateSerializer[64000];

	private readonly byte[] _inputDecodeBuffer = new byte[NetConstants.MaxUnreliableDataSize];

	private readonly NetDataReader _requestsReader = new NetDataReader();

	private readonly Queue<RemoteCallPacket> _pendingRPCs = new Queue<RemoteCallPacket>();

	private NetPlayer _syncForPlayer;

	private int _maxDataSize;

	private readonly AVLTree<InternalEntity> _changedEntities = new AVLTree<InternalEntity>();

	private byte[] _compressionBuffer = new byte[4096];

	/// <summary>
	/// Timeout after which player will receive baseline state if player cannot receive big partial state
	/// </summary>
	public float PlayerResyncTimeout = 4f;

	/// <summary>
	/// Rate at which server will make and send packets
	/// </summary>
	public readonly ServerSendRate SendRate;

	/// <summary>
	/// Add try catch to entity updates
	/// </summary>
	public bool SafeEntityUpdate;

	private ushort _minimalTick;

	private int _nextOrderNum;

	/// <summary>
	/// Network players count
	/// </summary>
	public int PlayersCount => _netPlayers.Count;

	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="typesMap">EntityTypesMap with registered entity types</param>
	/// <param name="packetHeader">Header byte that will be used for packets (to distinguish entity system packets)</param>
	/// <param name="framesPerSecond">Fixed framerate of game logic</param>
	/// <param name="sendRate">Send rate of server (depends on fps)</param>
	/// <param name="maxHistorySize">Maximum size of lag compensation history in ticks</param>
	public ServerEntityManager(EntityTypesMap typesMap, byte packetHeader, byte framesPerSecond, ServerSendRate sendRate, MaxHistorySize maxHistorySize = MaxHistorySize.Size32)
		: base(typesMap, NetworkMode.Server, packetHeader, maxHistorySize)
	{
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Expected O, but got Unknown
		InternalPlayerId = 0;
		_packetBuffer[0] = packetHeader;
		SendRate = sendRate;
		SetTickrate(framesPerSecond);
	}

	public override string GetCurrentFrameDebugInfo(DebugFrameModes modes)
	{
		if (!modes.HasFlagFast(DebugFrameModes.Server))
		{
			return string.Empty;
		}
		return $"[Server] Tick {_tick}";
	}

	public override void Reset()
	{
		base.Reset();
		_nextOrderNum = 0;
		_changedEntities.Clear();
		_pendingRPCs.Clear();
		_maxDataSize = 0;
	}

	/// <summary>
	/// Change SyncVar and RPC synchronization by SyncGroup for player
	/// constructor and destruction will be synchronized anyways
	/// works only on server
	/// </summary>
	/// <param name="forPlayer">For which player</param>
	/// <param name="entity">entity</param>
	/// <param name="syncGroup">syncGroup to enable/disable</param>
	/// <param name="enable">true - enable sync (if was disabled), disable otherwise</param>
	public void ToggleSyncGroup(byte forPlayer, EntityLogic entity, SyncGroup syncGroup, bool enable)
	{
		ToggleSyncGroup(GetPlayer(forPlayer), entity, syncGroup, enable);
	}

	/// <summary>
	/// Change SyncVar and RPC synchronization by SyncGroup for player
	/// constructor and destruction will be synchronized anyways
	/// works only on server
	/// </summary>
	/// <param name="forPlayer">For which player</param>
	/// <param name="entity">entity</param>
	/// <param name="syncGroup">syncGroup to enable/disable</param>
	/// <param name="enable">true - enable sync (if was disabled), disable otherwise</param>
	public void ToggleSyncGroup(NetPlayer forPlayer, EntityLogic entity, SyncGroup syncGroup, bool enable)
	{
		if (forPlayer == null || forPlayer.State == NetPlayerState.Removed || entity.IsDestroyed || entity.InternalOwnerId == forPlayer.Id)
		{
			return;
		}
		if (forPlayer.EntitySyncInfo.TryGetValue(entity, out var value))
		{
			if (value.IsGroupEnabled(syncGroup) != enable)
			{
				value.SetGroupEnabled(syncGroup, enable);
				value.LastChangedTick = _tick;
				if (enable)
				{
					MarkFieldsChanged(entity, SyncGroupUtils.ToSyncFlags(syncGroup));
				}
				else
				{
					_changedEntities.Add(entity);
					_stateSerializers[entity.Id].MarkChanged(_minimalTick, _tick);
				}
				forPlayer.EntitySyncInfo[entity] = value;
			}
		}
		else if (!enable)
		{
			value = new SyncGroupData(_tick);
			value.SetGroupEnabled(syncGroup, enabled: false);
			forPlayer.EntitySyncInfo.Add(entity, value);
			_changedEntities.Add(entity);
			_stateSerializers[entity.Id].MarkChanged(_minimalTick, _tick);
		}
	}

	/// <summary>
	/// Create and add new player
	/// </summary>
	/// <param name="peer">AbstractPeer to use</param>
	/// <returns>Newly created player, null if players count is maximum</returns>
	public NetPlayer AddPlayer(AbstractNetPeer peer)
	{
		if (_netPlayers.Count == 254)
		{
			return null;
		}
		if (peer.AssignedPlayer != null)
		{
			Logger.LogWarning("Peer already has an assigned player");
			return peer.AssignedPlayer;
		}
		if (_netPlayers.Count == 0)
		{
			_changedEntities.Clear();
		}
		NetPlayer netPlayer = new NetPlayer(peer, _playerIdQueue.GetNewId(), 30);
		_netPlayers.Set(netPlayer.Id, netPlayer);
		peer.AssignedPlayer = netPlayer;
		return netPlayer;
	}

	/// <summary>
	/// Get player by owner id
	/// </summary>
	/// <param name="ownerId">id of player owner (Entity.OwnerId)</param>
	/// <returns></returns>
	public NetPlayer GetPlayer(byte ownerId)
	{
		if (!_netPlayers.TryGetValue(ownerId, out var result))
		{
			return null;
		}
		return result;
	}

	/// <summary>
	/// Remove player using NetPeer.Tag (is you assigned it or used <see cref="M:LiteEntitySystem.ServerEntityManager.AddPlayer(LiteEntitySystem.Transport.AbstractNetPeer)" /> with assignToTag)
	/// </summary>
	/// <param name="player">player to remove</param>
	/// <returns>true if player removed successfully, false if player not found</returns>
	public bool RemovePlayer(AbstractNetPeer player)
	{
		return RemovePlayer(player.AssignedPlayer);
	}

	/// <summary>
	/// Remove player and it's owned entities
	/// </summary>
	/// <param name="player">player to remove</param>
	/// <returns>true if player removed successfully, false if player not found</returns>
	public bool RemovePlayer(NetPlayer player)
	{
		if (player == null || !_netPlayers.Contains(player.Id))
		{
			return false;
		}
		GetPlayerController(player)?.DestroyWithControlledEntity();
		bool result = _netPlayers.Remove(player.Id);
		_playerIdQueue.ReuseId(player.Id);
		player.State = NetPlayerState.Removed;
		if (_netPlayers.Count == 0)
		{
			RemoteCallPacket result2;
			while (_pendingRPCs.TryDequeue(out result2))
			{
				_maxDataSize -= result2.TotalSize;
				_rpcPool.Enqueue(result2);
			}
		}
		return result;
	}

	/// <summary>
	/// Returns controller owned by the player
	/// </summary>
	/// <param name="player">player</param>
	/// <returns>Instance if found, null if not</returns>
	public HumanControllerLogic GetPlayerController(AbstractNetPeer player)
	{
		return GetPlayerController(player.AssignedPlayer);
	}

	/// <summary>
	/// Returns controller owned by the player
	/// </summary>
	/// <param name="playerId">player</param>
	/// <returns>Instance if found, null if not</returns>
	public HumanControllerLogic GetPlayerController(byte playerId)
	{
		NetPlayer result;
		return GetPlayerController(_netPlayers.TryGetValue(playerId, out result) ? result : null);
	}

	/// <summary>
	/// Returns controller owned by the player
	/// </summary>
	/// <param name="player">player to remove</param>
	/// <returns>Instance if found, null if not</returns>
	public HumanControllerLogic GetPlayerController(NetPlayer player)
	{
		if (player == null || !_netPlayers.Contains(player.Id))
		{
			return null;
		}
		foreach (HumanControllerLogic entity in GetEntities<HumanControllerLogic>())
		{
			if (entity.InternalOwnerId.Value == player.Id)
			{
				return entity;
			}
		}
		return null;
	}

	/// <summary>
	/// Add new player controller entity
	/// </summary>
	/// <param name="owner">Player that owns this controller</param>
	/// <param name="initMethod">Method that will be called after entity construction</param>
	/// <typeparam name="T">Entity type</typeparam>
	/// <returns>Created entity or null in case of limit</returns>
	public T AddController<T>(NetPlayer owner, Action<T> initMethod = null) where T : HumanControllerLogic
	{
		return Add(delegate(T ent)
		{
			ent.InternalOwnerId.Value = owner.Id;
			initMethod?.Invoke(ent);
		});
	}

	/// <summary>
	/// Add new player controller entity and start controlling entityToControl
	/// </summary>
	/// <param name="owner">Player that owns this controller</param>
	/// <param name="entityToControl">pawn that will be controlled</param>
	/// <param name="initMethod">Method that will be called before entity OnConstructed</param>
	/// <typeparam name="T">Entity type</typeparam>
	/// <returns>Created entity or null in case of limit</returns>
	public T AddController<T>(NetPlayer owner, PawnLogic entityToControl, Action<T> initMethod = null) where T : HumanControllerLogic
	{
		return Add(delegate(T ent)
		{
			ent.InternalOwnerId.Value = owner.Id;
			ent.StartControl(entityToControl);
			initMethod?.Invoke(ent);
		});
	}

	/// <summary>
	/// Add new AI controller entity
	/// </summary>
	/// <param name="initMethod">Method that will be called before entity OnConstructed</param>
	/// <typeparam name="T">Entity type</typeparam>
	/// <returns>Created entity or null in case of limit</returns>
	public T AddAIController<T>(Action<T> initMethod = null) where T : AiControllerLogic
	{
		return Add(initMethod);
	}

	/// <summary>
	/// Add new entity
	/// </summary>
	/// <param name="initMethod">Method that will be called before entity OnConstructed</param>
	/// <typeparam name="T">Entity type</typeparam>
	/// <returns>Created entity or null in case of limit</returns>
	public T AddSingleton<T>(Action<T> initMethod = null) where T : SingletonEntityLogic
	{
		return Add(initMethod);
	}

	/// <summary>
	/// Add new entity
	/// </summary>
	/// <param name="initMethod">Method that will be called before entity OnConstructed</param>
	/// <typeparam name="T">Entity type</typeparam>
	/// <returns>Created entity or null in case of limit</returns>
	public T AddEntity<T>(Action<T> initMethod = null) where T : EntityLogic
	{
		return Add(initMethod);
	}

	/// <summary>
	/// Add new entity and set parent entity
	/// </summary>
	/// <param name="parent">Parent entity</param>
	/// <param name="initMethod">Method that will be called before entity OnConstructed</param>
	/// <typeparam name="T">Entity type</typeparam>
	/// <returns>Created entity or null in case of limit</returns>
	public T AddEntity<T>(EntityLogic parent, Action<T> initMethod = null) where T : EntityLogic
	{
		return Add(delegate(T e)
		{
			e.InternalOwnerId.Value = ((byte?)parent?.InternalOwnerId) ?? 0;
			e.SetParentInternal(parent);
			initMethod?.Invoke(e);
		});
	}

	/// <summary>
	/// Read data for player linked to AbstractNetPeer
	/// </summary>
	/// <param name="peer">Player that sent input</param>
	/// <param name="inData">incoming data with header</param>
	public DeserializeResult Deserialize(AbstractNetPeer peer, ReadOnlySpan<byte> inData)
	{
		return Deserialize(peer.AssignedPlayer, inData);
	}

	/// <summary>
	/// Read data from NetPlayer
	/// </summary>
	/// <param name="player">Player that sent input</param>
	/// <param name="inData">incoming data with header</param>
	public unsafe DeserializeResult Deserialize(NetPlayer player, ReadOnlySpan<byte> inData)
	{
		if (inData.Length == 0 || inData[0] != HeaderByte)
		{
			return DeserializeResult.HeaderCheckFailed;
		}
		inData = inData.Slice(1);
		if (inData.Length < 3)
		{
			Logger.LogWarning($"Invalid data received. Length < 3: {inData.Length}");
			return DeserializeResult.Error;
		}
		byte b = inData[0];
		inData = inData.Slice(1);
		switch (b)
		{
		case 5:
			if (inData.Length < 5)
			{
				Logger.LogError("size less than minRequest");
				return DeserializeResult.Error;
			}
			_pendingClientRequests.Enqueue(inData.ToArray());
			return DeserializeResult.Done;
		default:
			Logger.LogWarning($"[SEM] Unknown packet type: {b}");
			return DeserializeResult.Error;
		case 2:
		{
			int num = 0;
			foreach (HumanControllerLogic entity in GetEntities<HumanControllerLogic>())
			{
				if (entity.OwnerId == player.Id)
				{
					num += entity.MinInputDeltaSize;
					entity.DeltaDecodeInit();
				}
			}
			if (player.State == NetPlayerState.WaitingForFirstInput)
			{
				player.AvailableInput.Clear();
			}
			ushort num2 = BitConverter.ToUInt16(inData);
			inData = inData.Slice(2);
			while (inData.Length >= InputPacketHeader.Size)
			{
				InputInfo item = new InputInfo
				{
					Tick = num2
				};
				fixed (byte* ptr = inData)
				{
					item.Header = *(InputPacketHeader*)ptr;
				}
				inData = inData.Slice(InputPacketHeader.Size);
				bool flag = player.State == NetPlayerState.WaitingForFirstInput || Utils.SequenceDiff(item.Tick, player.LastReceivedTick) > 0;
				if (flag && inData.Length == 0)
				{
					player.AvailableInput.AddAndOverwrite(item, item.Tick);
					player.LastReceivedTick = item.Tick;
					break;
				}
				if (inData.Length < num)
				{
					Logger.LogError($"Bad input from: {player.Id} - {player.Peer} too small delta");
					return DeserializeResult.Error;
				}
				if (Utils.SequenceDiff(item.Header.StateA, base.Tick) > 0 || Utils.SequenceDiff(item.Header.StateB, base.Tick) > 0)
				{
					Logger.LogError($"Bad input from: {player.Id} - {player.Peer} invalid sequence");
					return DeserializeResult.Error;
				}
				item.Header.LerpMsec = Math.Clamp(item.Header.LerpMsec, 0f, 1f);
				if (Utils.SequenceDiff(item.Header.StateB, player.CurrentServerTick) > 0)
				{
					player.CurrentServerTick = item.Header.StateB;
					player.ServerTickChangedTime = DateTime.UtcNow;
				}
				num2++;
				foreach (HumanControllerLogic entity2 in GetEntities<HumanControllerLogic>())
				{
					if (entity2.OwnerId == player.Id)
					{
						Span<byte> span = new Span<byte>(_inputDecodeBuffer, 0, entity2.InputSize);
						span.Clear();
						int start = entity2.DeltaDecode(inData, span);
						inData = inData.Slice(start);
						if (flag)
						{
							entity2.AddIncomingInput(item.Tick, span);
						}
					}
				}
				if (flag)
				{
					player.AvailableInput.AddAndOverwrite(item, item.Tick);
					player.LastReceivedTick = item.Tick;
				}
			}
			if (player.State == NetPlayerState.WaitingForFirstInput)
			{
				player.State = NetPlayerState.WaitingForFirstInputProcess;
			}
			return DeserializeResult.Done;
		}
		}
	}

	private T Add<T>(Action<T> initMethod) where T : InternalEntity
	{
		if (EntityClassInfo<T>.ClassId == 0)
		{
			throw new Exception($"Unregistered entity type: {typeof(T)}");
		}
		ref EntityClassData reference = ref ClassDataDict[EntityClassInfo<T>.ClassId];
		if (_entityIdQueue.AvailableIds == 0)
		{
			Logger.Log($"Cannot add entity. Max entity count reached: {64000}");
			return null;
		}
		ushort newId = _entityIdQueue.GetNewId();
		ref StateSerializer reference2 = ref _stateSerializers[newId];
		byte[] ioBuffer = reference.AllocateDataCache();
		reference2.AllocateMemory(ref reference, ioBuffer);
		T val = AddEntity<T>(new EntityParams(newId, new EntityDataHeader(reference.ClassId, reference2.NextVersion, ++_nextOrderNum), this, ioBuffer));
		reference2.Init(val, _tick);
		RemoteCallPacket packet = reference2.MakeNewRPC();
		initMethod?.Invoke(val);
		reference2.RefreshNewRPC(packet);
		ConstructEntity(val);
		reference2.MakeConstructedRPC(null);
		_changedEntities.Add(val);
		_maxDataSize += reference2.MaximumSize;
		return val;
	}

	internal override void OnEntityDestroyed(InternalEntity e)
	{
		base.OnEntityDestroyed(e);
		if (_netPlayers.Count == 0)
		{
			RemoveEntity(e);
		}
		else
		{
			_stateSerializers[e.Id].MakeDestroyedRPC(_tick);
		}
	}

	protected unsafe override void OnLogicTick()
	{
		while (_pendingClientRequests.Count > 0)
		{
			_requestsReader.SetSource(_pendingClientRequests.Dequeue());
			ushort uShort = _requestsReader.GetUShort();
			byte version = _requestsReader.GetByte();
			if (TryGetEntityById<HumanControllerLogic>(new EntitySharedReference(uShort, version), out var entity))
			{
				entity.ReadClientRequest(_requestsReader);
			}
		}
		_minimalTick = _tick;
		bool flag = false;
		int count = _netPlayers.Count;
		for (int i = 0; i < count; i++)
		{
			NetPlayer byIndex = _netPlayers.GetByIndex(i);
			if (byIndex.FirstBaselineSent)
			{
				_minimalTick = ((Utils.SequenceDiff(byIndex.StateATick, _minimalTick) < 0) ? byIndex.StateATick : _minimalTick);
			}
			if (byIndex.State == NetPlayerState.RequestBaseline)
			{
				flag = true;
			}
			else
			{
				if (byIndex.AvailableInput.Count == 0)
				{
					continue;
				}
				InputInfo inputData = byIndex.AvailableInput.ExtractMin();
				byIndex.LoadInputInfo(inputData);
				if (byIndex.State == NetPlayerState.WaitingForFirstInputProcess)
				{
					byIndex.State = NetPlayerState.Active;
				}
				foreach (HumanControllerLogic entity2 in GetEntities<HumanControllerLogic>())
				{
					if (entity2.InternalOwnerId.Value == byIndex.Id)
					{
						entity2.ApplyIncomingInput(inputData.Tick);
					}
				}
			}
		}
		if (SafeEntityUpdate)
		{
			foreach (InternalEntity aliveEntity in AliveEntities)
			{
				if (!aliveEntity.IsDestroyed)
				{
					aliveEntity.SafeUpdate();
				}
			}
		}
		else
		{
			foreach (InternalEntity aliveEntity2 in AliveEntities)
			{
				if (!aliveEntity2.IsDestroyed)
				{
					aliveEntity2.Update();
				}
			}
		}
		ExecuteLateConstruct();
		ExecuteLocalSingletonsLateUpdate();
		foreach (RemoteCallPacket pendingRPC in _pendingRPCs)
		{
			if (pendingRPC.Header.Id == 2 && pendingRPC.Header.Tick == _tick)
			{
				_stateSerializers[pendingRPC.Header.EntityId].RefreshConstructedRPC(pendingRPC);
			}
		}
		foreach (EntityLogic lagCompensatedEntity in LagCompensatedEntities)
		{
			ClassDataDict[lagCompensatedEntity.ClassId].WriteHistory(lagCompensatedEntity, _tick);
		}
		if (count == 0 || (int)_tick % (int)SendRate != 0)
		{
			return;
		}
		DateTime utcNow = DateTime.UtcNow;
		RemoteCallPacket result;
		while (_pendingRPCs.TryPeek(out result) && Utils.SequenceDiff(result.Header.Tick, _minimalTick) < 0)
		{
			_maxDataSize -= result.TotalSize;
			_rpcPool.Enqueue(_pendingRPCs.Dequeue());
		}
		int num = sizeof(BaselineDataHeader) + _maxDataSize;
		if (_packetBuffer.Length < num)
		{
			_packetBuffer = new byte[num];
		}
		if (flag)
		{
			int num2 = LZ4Codec.MaximumOutputSize(_packetBuffer.Length);
			if (_compressionBuffer.Length < num2)
			{
				_compressionBuffer = new byte[num2];
			}
		}
		fixed (byte* packetBuffer = _packetBuffer)
		{
			fixed (byte* compressionBuffer = _compressionBuffer)
			{
				for (int j = 0; j < count; j++)
				{
					NetPlayer byIndex2 = _netPlayers.GetByIndex(j);
					_syncForPlayer = null;
					int num3 = 0;
					RPCHeader prevHeader = default(RPCHeader);
					if (byIndex2.State == NetPlayerState.RequestBaseline)
					{
						int position = 0;
						if (!byIndex2.FirstBaselineSent)
						{
							byIndex2.FirstBaselineSent = true;
							_syncForPlayer = byIndex2;
							foreach (InternalEntity entity3 in GetEntities<InternalEntity>())
							{
								if (_stateSerializers[entity3.Id].ShouldSync(byIndex2.Id, includeDestroyed: false))
								{
									RemoteCallPacket packet = _stateSerializers[entity3.Id].MakeNewRPC();
									_stateSerializers[entity3.Id].RefreshNewRPC(packet);
									_stateSerializers[entity3.Id].MakeConstructedRPC(byIndex2);
								}
							}
							_syncForPlayer = null;
							foreach (RemoteCallPacket pendingRPC2 in _pendingRPCs)
							{
								if (pendingRPC2.OnlyForPlayer == byIndex2)
								{
									InternalEntity internalEntity = EntitiesDict[pendingRPC2.Header.EntityId];
									if (pendingRPC2.AllowToSendForPlayer(byIndex2.Id, internalEntity.OwnerId))
									{
										pendingRPC2.WriteTo(packetBuffer, ref position, ref prevHeader);
									}
								}
							}
							num3 = position;
						}
						else
						{
							foreach (RemoteCallPacket pendingRPC3 in _pendingRPCs)
							{
								if (ShouldSendRPC(pendingRPC3, byIndex2, isBaseline: true))
								{
									pendingRPC3.WriteTo(packetBuffer, ref position, ref prevHeader);
								}
							}
							num3 = position;
							foreach (InternalEntity entity4 in GetEntities<InternalEntity>())
							{
								_stateSerializers[entity4.Id].MakeDiff(byIndex2, _minimalTick, packetBuffer, ref position);
							}
						}
						*(BaselineDataHeader*)compressionBuffer = new BaselineDataHeader
						{
							UserHeader = HeaderByte,
							PacketType = 3,
							OriginalLength = position,
							Tick = _tick,
							PlayerId = byIndex2.Id,
							SendRate = (byte)SendRate,
							Tickrate = base.Tickrate,
							EventsSize = num3
						};
						int num4 = LZ4Codec.Encode(packetBuffer, position, compressionBuffer + sizeof(BaselineDataHeader), _compressionBuffer.Length - sizeof(BaselineDataHeader), (LZ4Level)0);
						byIndex2.Peer.SendReliableOrdered(new ReadOnlySpan<byte>(compressionBuffer, sizeof(BaselineDataHeader) + num4));
						byIndex2.StateATick = _tick;
						byIndex2.StateBTick = _tick;
						byIndex2.CurrentServerTick = _tick;
						byIndex2.State = NetPlayerState.WaitingForFirstInput;
						byIndex2.ServerTickChangedTime = DateTime.UtcNow;
						Logger.Log($"[SEM] SendWorld to player {byIndex2.Id}. orig: {position} b, compressed: {num4} b, ExecutedTick: {_tick}");
					}
					else
					{
						if (byIndex2.State != NetPlayerState.Active)
						{
							continue;
						}
						if ((utcNow - byIndex2.ServerTickChangedTime).TotalSeconds > (double)PlayerResyncTimeout)
						{
							Logger.Log($"P:{byIndex2.Id} Request baseline {_tick} because timeout");
							byIndex2.State = NetPlayerState.RequestBaseline;
							continue;
						}
						DiffPartHeader* ptr = (DiffPartHeader*)packetBuffer;
						ptr->UserHeader = HeaderByte;
						ptr->Part = 0;
						ptr->Tick = _tick;
						int writePosition = sizeof(DiffPartHeader);
						ushort num5 = (ushort)(byIndex2.Peer.GetMaxUnreliablePacketSize() - sizeof(LastPartData));
						foreach (RemoteCallPacket pendingRPC4 in _pendingRPCs)
						{
							if (ShouldSendRPC(pendingRPC4, byIndex2, isBaseline: false))
							{
								num3 += pendingRPC4.WriteTo(packetBuffer, ref writePosition, ref prevHeader);
								CheckOverflowAndSend(byIndex2, ptr, packetBuffer, ref writePosition, num5);
								if (byIndex2.State == NetPlayerState.RequestBaseline)
								{
									break;
								}
							}
						}
						if (byIndex2.State == NetPlayerState.RequestBaseline)
						{
							continue;
						}
						foreach (InternalEntity changedEntity in _changedEntities)
						{
							ref StateSerializer reference = ref _stateSerializers[changedEntity.Id];
							if (Utils.SequenceDiff(reference.LastChangedTick, _minimalTick) <= 0)
							{
								_changedEntities.Remove(changedEntity);
								if (changedEntity.IsDestroyed && !changedEntity.IsRemoved)
								{
									if (changedEntity.UpdateOrderNum == _nextOrderNum)
									{
										_nextOrderNum = (GetEntities<InternalEntity>().TryGetMax(out var element) ? element.UpdateOrderNum : 0);
									}
									_entityIdQueue.ReuseId(changedEntity.Id);
									_maxDataSize -= reference.MaximumSize;
									reference.Free();
									RemoveEntity(changedEntity);
								}
							}
							else if (reference.MakeDiff(byIndex2, _minimalTick, packetBuffer, ref writePosition))
							{
								CheckOverflowAndSend(byIndex2, ptr, packetBuffer, ref writePosition, num5);
								if (byIndex2.State == NetPlayerState.RequestBaseline)
								{
									break;
								}
							}
						}
						if (byIndex2.State != NetPlayerState.RequestBaseline)
						{
							ptr->PacketType = 4;
							*(LastPartData*)(packetBuffer + writePosition) = new LastPartData
							{
								LastProcessedTick = byIndex2.LastProcessedTick,
								LastReceivedTick = byIndex2.LastReceivedTick,
								Mtu = num5,
								BufferedInputsCount = (byte)byIndex2.AvailableInput.Count,
								EventsSize = num3
							};
							writePosition += sizeof(LastPartData);
							byIndex2.Peer.SendUnreliable(new ReadOnlySpan<byte>(packetBuffer, writePosition));
						}
					}
				}
			}
		}
		_netPlayers.GetByIndex(0).Peer.TriggerSend();
		unsafe void CheckOverflowAndSend(NetPlayer player, DiffPartHeader* header, byte* ptr2, ref int reference2, int maxPartSize)
		{
			for (int num6 = reference2 - maxPartSize; num6 > 0; num6 = reference2 - maxPartSize)
			{
				if (header->Part == byte.MaxValue)
				{
					Logger.Log($"P:{player.Id} Request baseline {_tick} because state size");
					player.State = NetPlayerState.RequestBaseline;
					break;
				}
				header->PacketType = 1;
				player.Peer.SendUnreliable(new ReadOnlySpan<byte>(ptr2, maxPartSize));
				byte* part = &header->Part;
				(*part)++;
				RefMagic.CopyBlock(ptr2 + sizeof(DiffPartHeader), ptr2 + maxPartSize, (uint)num6);
				reference2 = sizeof(DiffPartHeader) + num6;
			}
		}
		bool ShouldSendRPC(RemoteCallPacket rpcNode, NetPlayer player, bool isBaseline)
		{
			if (rpcNode.OnlyForPlayer != null && rpcNode.OnlyForPlayer != player)
			{
				return false;
			}
			if (Utils.SequenceDiff(rpcNode.Header.Tick, player.CurrentServerTick) <= 0)
			{
				return false;
			}
			ref StateSerializer reference2 = ref _stateSerializers[rpcNode.Header.EntityId];
			if (!reference2.ShouldSync(player.Id, includeDestroyed: true))
			{
				return false;
			}
			InternalEntity internalEntity2 = EntitiesDict[rpcNode.Header.EntityId];
			if (!rpcNode.AllowToSendForPlayer(player.Id, internalEntity2.InternalOwnerId.Value))
			{
				return false;
			}
			if (internalEntity2.InternalOwnerId.Value != player.Id && internalEntity2 is EntityLogic key && player.EntitySyncInfo.TryGetValue(key, out var value) && SyncGroupUtils.IsRPCDisabled(value.EnabledGroups, rpcNode.ExecuteFlags))
			{
				return false;
			}
			switch ((InternalRPCType)rpcNode.Header.Id)
			{
			case InternalRPCType.NewOwned:
				if (internalEntity2.InternalOwnerId.Value != player.Id || isBaseline)
				{
					rpcNode.Header.Id = 0;
				}
				break;
			case InternalRPCType.New:
				if (internalEntity2.InternalOwnerId.Value == player.Id && !isBaseline)
				{
					rpcNode.Header.Id = 1;
				}
				break;
			case InternalRPCType.Construct:
				reference2.RefreshSyncGroupsVariable(player, new Span<byte>(rpcNode.Data));
				break;
			}
			return true;
		}
	}

	internal unsafe override void EntityFieldChanged<T>(InternalEntity entity, ushort fieldId, ref T newValue, ref T oldValue, bool skipOnSync)
	{
		if (!entity.IsRemoved && !(entity is AiControllerLogic))
		{
			_changedEntities.Add(entity);
			_stateSerializers[entity.Id].UpdateFieldValue(fieldId, _minimalTick, _tick, ref newValue);
			ref EntityFieldInfo reference = ref entity.ClassData.Fields[fieldId];
			if (!skipOnSync && (reference.OnSyncFlags & BindOnChangeFlags.ExecuteOnServer) != 0)
			{
				T val = oldValue;
				reference.OnSync(reference.GetTargetObject(entity), new ReadOnlySpan<byte>(&val, reference.IntSize));
			}
		}
	}

	internal void MarkFieldsChanged(InternalEntity entity, SyncFlags onlyWithFlags)
	{
		_changedEntities.Add(entity);
		_stateSerializers[entity.Id].MarkFieldsChanged(_minimalTick, _tick, onlyWithFlags);
	}

	internal RemoteCallPacket AddRemoteCall(InternalEntity entity, ushort rpcId, ExecuteFlags flags)
	{
		if (PlayersCount == 0 || entity.IsRemoved || entity is AiControllerLogic || (flags & ExecuteFlags.SendToAll) == 0)
		{
			return null;
		}
		RemoteCallPacket remoteCallPacket = ((_rpcPool.Count > 0) ? _rpcPool.Dequeue() : new RemoteCallPacket());
		remoteCallPacket.Init(_syncForPlayer, entity, _tick, 0, rpcId, flags);
		_pendingRPCs.Enqueue(remoteCallPacket);
		_maxDataSize += remoteCallPacket.TotalSize;
		return remoteCallPacket;
	}

	internal void NotifyRPCResized(int prevTotalSize, int newTotalSize)
	{
		_maxDataSize -= prevTotalSize;
		_maxDataSize += newTotalSize;
	}

	internal unsafe RemoteCallPacket AddRemoteCall<T>(InternalEntity entity, ReadOnlySpan<T> value, ushort rpcId, ExecuteFlags flags) where T : unmanaged
	{
		if (PlayersCount == 0 || entity.IsRemoved || entity is AiControllerLogic || (flags & ExecuteFlags.SendToAll) == 0)
		{
			return null;
		}
		RemoteCallPacket remoteCallPacket = ((_rpcPool.Count > 0) ? _rpcPool.Dequeue() : new RemoteCallPacket());
		int num = sizeof(T) * value.Length;
		if (num > 65535)
		{
			Logger.LogError($"DataSize on rpc: {rpcId}, entity: {entity} is more than {ushort.MaxValue}");
			return null;
		}
		remoteCallPacket.Init(_syncForPlayer, entity, _tick, (ushort)num, rpcId, flags);
		if (value.Length > 0)
		{
			fixed (T* ptr = value)
			{
				void* source = ptr;
				fixed (byte* data = remoteCallPacket.Data)
				{
					void* destination = data;
					RefMagic.CopyBlock(destination, source, (uint)num);
				}
			}
		}
		_pendingRPCs.Enqueue(remoteCallPacket);
		_maxDataSize += remoteCallPacket.TotalSize;
		return remoteCallPacket;
	}
}
