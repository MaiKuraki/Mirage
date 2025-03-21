---
sidebar_position: 2
---
# Client RPC

ClientRpcs are sent from [NetworkBehaviours](/docs/reference/Mirage/NetworkBehaviour) on the server to Behaviours on the client. They can be sent from any [NetworkBehaviour](/docs/reference/Mirage/NetworkBehaviour) that has been spawned.

To make a function into a ClientRpc add [`[ClientRpc]`](/docs/reference/Mirage/ClientRpcAttribute) directly above the function.

```cs
[ClientRpc]
public void MyRpcFunction() 
{
    // Code to invoke on client
}
```

ClientRpc functions can't be static and must return `void`.

## RpcTarget

There are 3 target modes for ClientRpc:
- Observers (default)
- Owner
- Player

### RpcTarget.Observers

This is the default target.

This will send the RPC message to only the observers of an object according to its [Network Visibility](/docs/guides/network-visibility). If there is no Network Visibility on the object it will send to all players.

### RpcTarget.Owner

This will send the RPC message to only the owner of the object.

### RpcTarget.Player

This will send the RPC message to the [`NetworkPlayer`](/docs/reference/Mirage/NetworkPlayer) that is passed into the call.

```cs
[ClientRpc(target = RpcTarget.Player)]
public void MyRpcFunction(NetworkPlayer target) 
{
    // Code to invoke on client
}
```

Mirage will use the `NetworkPlayer target` to know where to send it, but it will not send the `target` value. Because of this, its value will always be null for the client.

## Exclude owner

You may want to exclude the owner client when calling a ClientRpc. This is done with the `excludeOwner` option: `[ClientRpc(excludeOwner = true)]`.


## Channel

RPC can be sent using either the Reliable or Unreliable channels. `[ClientRpc(channel = Channel.Reliable)]`

### Returning values

ClientRpcs can return values only if RpcTarget is `Player` or `Owner`. It can take a long time for the client to reply, so they must return a UniTask which the server can await.

To return a value, add a return value using `UniTask<MyReturnType>` where `MyReturnType` is any [supported Mirage type](/docs/guides/serialization/data-types). In the client, you can make your method async,  or you can use `UniTask.FromResult(myResult);`. For example:

{{{ Path:'Snippets/RpcReply.cs' Name:'client-rpc-reply' }}}

# Examples 

``` cs
public class Player : NetworkBehaviour
{
    private int health;

    public void TakeDamage(int amount)
    {
        if (!IsServer)
        {
            return;
        }

        health -= amount;
        Damage(amount);
    }

    [ClientRpc]
    private void Damage(int amount)
    {
        Debug.Log("Took damage:" + amount);
    }
}
```

When running a game as a host with a local client, ClientRpc calls will be invoked on the local client even though it is in the same process as the server. So the behaviors of local and remote clients are the same for ClientRpc calls.

You can also specify which client gets the call with the `target` parameter. 

If you only want the client that owns the object to be called,  use `[ClientRpc(target = RpcTarget.Owner)]` or you can specify which client gets the message by using `[ClientRpc(target = RpcTarget.Player)]` and passing the player as a parameter.  For example:

``` cs
public class Player : NetworkBehaviour
{
    private int health;

    [Server]
    private void Magic(GameObject target, int damage)
    {
        target.GetComponent<Player>().health -= damage;

        NetworkIdentity opponentIdentity = target.GetComponent<NetworkIdentity>();
        DoMagic(opponentIdentity.Owner, damage);
    }

    [ClientRpc(target = RpcTarget.Player)]
    public void DoMagic(INetworkPlayer target, int damage)
    {
        // This will appear on the opponent's client, not the attacking player's
        Debug.Log($"Magic Damage = {damage}");
    }

    [Server]
    private void HealMe()
    {
        health += 10;
        Healed(10);
    }

    [ClientRpc(target = RpcTarget.Owner)]
    public void Healed(int amount)
    {
        // No NetworkPlayer parameter, so it goes to owner
        Debug.Log($"Health increased by {amount}");
    }
}
```
