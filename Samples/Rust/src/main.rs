use prost::Message;
use std::io::Write;
use std::net::TcpStream;

#[derive(Clone, PartialEq, Message)]
pub struct RequestMessage {
    #[prost(int32, tag = "1")]
    pub integer: i32,
    #[prost(float, tag = "2")]
    pub float: f32,
    #[prost(string, tag = "3")]
    pub string: String,
    #[prost(bool, tag = "4")]
    pub boolean: bool,
}

fn build_payload(msg: RequestMessage) -> Vec<u8> {
    let mut msg_buf = Vec::new();
    msg.encode(&mut msg_buf).unwrap();

    let type_id: u16 = 1; // Matches [Message(1)] in RequestMessage.cs
    let data_type_len = 2u32; // size of type id
    let data_len = msg_buf.len() as u32;
    let payload_size = 4 + data_type_len as usize + 4 + msg_buf.len();

    let mut buffer = Vec::with_capacity(4 + payload_size);
    buffer.extend_from_slice(&(payload_size as u32).to_le_bytes());
    buffer.extend_from_slice(&data_type_len.to_le_bytes());
    buffer.extend_from_slice(&type_id.to_le_bytes());
    buffer.extend_from_slice(&data_len.to_le_bytes());
    buffer.extend_from_slice(&msg_buf);
    buffer
}

fn main() -> std::io::Result<()> {
    let msg = RequestMessage {
        integer: 42,
        float: 1.23,
        string: "hello from Rust".to_string(),
        boolean: true,
    };

    let payload = build_payload(msg);

    let mut stream = TcpStream::connect("127.0.0.1:9900")?;
    stream.write_all(&payload)?;
    Ok(())
}
