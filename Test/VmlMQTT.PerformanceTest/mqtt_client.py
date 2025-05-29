import paho.mqtt.client as mqtt
import json
import time
import logging
import random
import threading
from typing import Dict, Any, List, Callable, Optional

# Thiết lập logging
logger = logging.getLogger("mqtt_client")

class MqttClientMetrics:
    """Lưu trữ các số liệu của MQTT client"""
    
    def __init__(self):
        """Khởi tạo đối tượng metrics"""
        self.lock = threading.Lock()
        self.connection_time = None
        self.is_connected = False
        self.connect_result = None
        self.messages_published = 0
        self.messages_received = 0
        self.publish_times = []
        self.last_error = None
        self.send_message_ids = {}  # Map của message id -> thời gian gửi

class MqttClient:
    """Client MQTT để kết nối và test pub/sub"""
    
    def __init__(self, connection_info: Dict[str, Any]):
        """
        Khởi tạo MQTT client
        
        Args:
            connection_info: Thông tin kết nối MQTT (clientId, username, password, host, port)
        """
        self.connection_info = connection_info
        
        # Đảm bảo dùng refreshToken làm clientId và username
        # Và dùng accessToken từ startSession làm password
        self.client_id = connection_info.get("clientId")
        if not self.client_id:
            logger.error("Missing clientId (refreshToken) in connection info")
            raise ValueError("MQTT connection info missing clientId (refreshToken)")
            
        self.metrics = MqttClientMetrics()
        
        # Tạo client với userdata chứa thông tin và metrics
        self.client = mqtt.Client(
            client_id=self.client_id,
            userdata={"connection_info": connection_info, "metrics": self.metrics},
            protocol=mqtt.MQTTv311,
            clean_session=True
        )
        
        # Thiết lập callbacks
        self.client.on_connect = self._on_connect
        self.client.on_disconnect = self._on_disconnect
        self.client.on_message = self._on_message
        self.client.on_publish = self._on_publish
        
        # Kiểm tra và thiết lập username/password
        username = connection_info.get("username")
        password = connection_info.get("password")
        
        if not username or not password:
            logger.error("MQTT credentials missing (username=refreshToken, password=sessionToken)")
            raise ValueError("MQTT credentials incomplete")
            
        # Thiết lập username/password
        self.client.username_pw_set(
            username=username,
            password=password
        )
    
    @staticmethod
    def _on_connect(client, userdata, flags, rc):
        """Callback khi kết nối thành công hoặc thất bại"""
        metrics = userdata["metrics"]
        connection_info = userdata["connection_info"]
        
        with metrics.lock:
            metrics.connect_result = rc
            if rc == 0:
                metrics.is_connected = True
                metrics.connection_time = time.time() - metrics.connection_time
                logger.info(f"Kết nối thành công: {connection_info.get('clientId')} - {metrics.connection_time:.4f}s")
            else:
                metrics.is_connected = False
                metrics.last_error = f"Lỗi kết nối: {rc}"
                logger.error(f"Kết nối thất bại: {connection_info.get('clientId')} - Lỗi: {rc}")
    
    @staticmethod
    def _on_disconnect(client, userdata, rc):
        """Callback khi ngắt kết nối"""
        metrics = userdata["metrics"]
        
        with metrics.lock:
            metrics.is_connected = False
            if rc != 0:
                metrics.last_error = f"Ngắt kết nối với lỗi: {rc}"
                logger.warning(f"Ngắt kết nối không mong muốn, rc={rc}")
    
    @staticmethod
    def _on_message(client, userdata, msg):
        """Callback khi nhận được tin nhắn"""
        metrics = userdata["metrics"]
        
        with metrics.lock:
            metrics.messages_received += 1
            if metrics.messages_received % 100 == 0:
                logger.info(f"Đã nhận {metrics.messages_received} tin nhắn")
    
    @staticmethod
    def _on_publish(client, userdata, mid):
        """Callback khi publish thành công"""
        metrics = userdata["metrics"]
        
        with metrics.lock:
            if mid in metrics.send_message_ids:
                start_time = metrics.send_message_ids[mid]
                publish_time = time.time() - start_time
                metrics.publish_times.append(publish_time)
                metrics.messages_published += 1
                
                # Xóa mid đã xử lý
                del metrics.send_message_ids[mid]
                
                if metrics.messages_published % 100 == 0:
                    logger.info(f"Đã publish {metrics.messages_published} tin nhắn")
    
    def connect(self, timeout: int = 30) -> bool:
        """
        Kết nối đến MQTT broker
        
        Args:
            timeout: Thời gian timeout (giây)
            
        Returns:
            bool: True nếu kết nối thành công, False nếu thất bại
        """
        try:
            # Đảm bảo có đủ thông tin kết nối
            host = self.connection_info.get("host")
            port = self.connection_info.get("port")
            
            if not host:
                self.metrics.last_error = "Thiếu thông tin host"
                logger.error(f"Client {self.client_id}: Thiếu thông tin host")
                return False
            
            if not port:
                port = 1883
                logger.warning(f"Client {self.client_id}: Sử dụng port mặc định 1883")
            
            logger.info(f"Client {self.client_id}: Kết nối đến {host}:{port}")
            
            # Ghi lại thời điểm bắt đầu kết nối
            self.metrics.connection_time = time.time()
            
            # Kết nối đến broker
            self.client.connect(host, port, keepalive=60)
            
            # Bắt đầu loop trong thread riêng
            self.client.loop_start()
            
            # Chờ kết nối thành công hoặc timeout
            start_time = time.time()
            while time.time() - start_time < timeout:
                if self.metrics.connect_result is not None:
                    break
                time.sleep(0.1)
            
            # Kiểm tra kết quả kết nối
            if self.metrics.connect_result == 0:
                logger.info(f"Client {self.client_id}: Kết nối thành công sau {self.metrics.connection_time:.2f}s")
                # Đăng ký các topic
                self._subscribe_to_topics()
                return True
            else:
                error_msg = f"Lỗi kết nối MQTT, mã lỗi: {self.metrics.connect_result}"
                self.metrics.last_error = error_msg
                logger.error(f"Client {self.client_id}: {error_msg}")
                return False
                
        except Exception as e:
            self.metrics.last_error = f"Lỗi kết nối: {str(e)}"
            logger.error(f"Client {self.client_id}: Lỗi khi kết nối đến MQTT broker: {str(e)}")
            return False
    
    def _subscribe_to_topics(self):
        """Đăng ký các topic từ thông tin kết nối"""
        sub_topics = self.connection_info.get("subTopics", [])
        if not sub_topics:
            logger.warning(f"Client {self.client_id} không có topic nào để đăng ký")
            return
            
        logger.info(f"Client {self.client_id} đăng ký {len(sub_topics)} topics")
        for topic in sub_topics:
            result, mid = self.client.subscribe(topic, qos=1)
            if result == mqtt.MQTT_ERR_SUCCESS:
                logger.debug(f"Đã đăng ký topic: {topic} với mid={mid}")
            else:
                logger.error(f"Không thể đăng ký topic: {topic}, lỗi={result}")
    
    def publish_message(self, message_size: int = 100, qos: int = 1) -> bool:
        """
        Publish tin nhắn đến một topic ngẫu nhiên
        
        Args:
            message_size: Kích thước tin nhắn (bytes)
            qos: Quality of Service (0, 1, 2)
            
        Returns:
            bool: True nếu publish thành công, False nếu thất bại
        """
        if not self.metrics.is_connected:
            return False
        
        try:
            # Chọn topic
            pub_topics = self.connection_info.get("pubTopics", [])
            if not pub_topics:
                return False
            
            topic = random.choice(pub_topics)
            
            # Tạo tin nhắn với kích thước chỉ định
            message = {
                "client_id": self.client_id,
                "phone": self.connection_info.get("phone", "unknown"),
                "timestamp": time.time(),
                "message": f"Test message {random.randint(1000, 9999)}",
                "data": "X" * max(1, message_size - 150)  # Đảm bảo kích thước xấp xỉ
            }
            
            # Publish tin nhắn
            payload = json.dumps(message)
            result = self.client.publish(topic, payload, qos=qos)
            
            # Lưu message id và thời gian bắt đầu publish
            mid = result.mid
            with self.metrics.lock:
                self.metrics.send_message_ids[mid] = time.time()
            
            return True
            
        except Exception as e:
            logger.error(f"Lỗi khi publish tin nhắn: {str(e)}")
            return False
    
    def disconnect(self):
        """Ngắt kết nối từ MQTT broker"""
        try:
            if self.metrics.is_connected:
                self.client.disconnect()
            self.client.loop_stop()
        except Exception as e:
            logger.error(f"Lỗi khi ngắt kết nối MQTT: {str(e)}")
    
    def get_metrics(self) -> Dict[str, Any]:
        """
        Lấy metrics hiện tại
        
        Returns:
            Dict: Metrics của client
        """
        with self.metrics.lock:
            avg_publish_time = 0
            if self.metrics.publish_times:
                avg_publish_time = sum(self.metrics.publish_times) / len(self.metrics.publish_times)
            
            return {
                "client_id": self.client_id,
                "is_connected": self.metrics.is_connected,
                "connection_time": self.metrics.connection_time,
                "messages_published": self.metrics.messages_published,
                "messages_received": self.metrics.messages_received,
                "avg_publish_time": avg_publish_time,
                "last_error": self.metrics.last_error
            }
